import { createHubConnection, reconnectDelaysMs } from './connection.js';
import { resolveDetail } from './detail.js';
import { Emitter } from './emitter.js';
import { deepEqual } from './equality.js';
import type {
  ClientEvents,
  ClientOptions,
  ConnectionState,
  EvaluationContext,
  FlagDetail,
  FlagEvaluation,
  FlagForgeClient,
  HubConnectionFactory,
  HubConnectionLike,
  Logger,
} from './types.js';

const flagsChangedDebounceMs = 250;
const defaultPollIntervalMs = 30_000;
const defaultReadyTimeoutMs = 5_000;
const backgroundReconnectMs = 30_000;

interface EvaluateResponse {
  environmentVersion: number;
  flags: Record<string, FlagEvaluation>;
}

const defaultLogger: Logger = {
  warn: (message, ...details) => console.warn(`[flagforge] ${message}`, ...details),
  error: (message, ...details) => console.error(`[flagforge] ${message}`, ...details),
};

/**
 * Creates a client that evaluates every flag for one context and keeps the values current. An application must
 * never break because flags are unavailable: getters return the caller's default until values arrive, when a flag is
 * missing, or when its value has the wrong type.
 */
export function createClient(options: ClientOptions): FlagForgeClient {
  return new Client(options);
}

class Client implements FlagForgeClient {
  private readonly baseUrl: string;
  private readonly sdkKey: string;
  private readonly streaming: boolean;
  private readonly pollIntervalMs: number;
  private readonly logger: Logger;
  private readonly fetchImpl: typeof fetch;
  private readonly hubFactory: HubConnectionFactory;
  private readonly emitter: Emitter<ClientEvents>;
  private readonly subscribers = new Set<() => void>();
  private readonly inflight = new Set<AbortController>();
  private readonly readyPromise: Promise<void>;

  private resolveReady: () => void = () => undefined;
  private currentContext: EvaluationContext;
  private evaluations: ReadonlyMap<string, FlagEvaluation> = new Map();
  private hasEvaluated = false;
  private state: ConnectionState = 'connecting';
  private requestSequence = 0;
  private appliedSequence = 0;
  private closed = false;
  private hub: HubConnectionLike | undefined;
  private readyTimer: ReturnType<typeof setTimeout> | undefined;
  private debounceTimer: ReturnType<typeof setTimeout> | undefined;
  private reconnectTimer: ReturnType<typeof setTimeout> | undefined;
  private pollTimer: ReturnType<typeof setInterval> | undefined;

  constructor(options: ClientOptions) {
    if (!options.baseUrl) {
      throw new TypeError('FlagForge: baseUrl is required, for example window.location.origin.');
    }

    if (!options.sdkKey) {
      throw new TypeError('FlagForge: sdkKey is required.');
    }

    assertContext(options.context);
    this.baseUrl = options.baseUrl.replace(/\/+$/, '');
    this.sdkKey = options.sdkKey;
    this.currentContext = options.context;
    this.streaming = options.streaming ?? true;
    this.pollIntervalMs = options.pollIntervalMs ?? defaultPollIntervalMs;
    this.logger = options.logger ?? defaultLogger;
    this.fetchImpl = options.fetch ?? ((input, init) => globalThis.fetch(input, init));
    this.hubFactory = options.hubConnectionFactory ?? createHubConnection;
    this.emitter = new Emitter<ClientEvents>((error) =>
      this.logger.error?.('An event listener threw', error),
    );
    this.readyPromise = new Promise<void>((resolve) => {
      this.resolveReady = resolve;
    });
    this.readyTimer = setTimeout(
      () => this.onReadyTimeout(options.readyTimeoutMs ?? defaultReadyTimeoutMs),
      options.readyTimeoutMs ?? defaultReadyTimeoutMs,
    );

    void this.refresh();
    if (this.streaming) {
      void this.startStreaming();
    } else {
      this.setState('polling');
      this.startPolling();
    }
  }

  get isReady(): boolean {
    return this.hasEvaluated;
  }

  get connectionState(): ConnectionState {
    return this.state;
  }

  get context(): EvaluationContext {
    return this.currentContext;
  }

  readonly subscribe = (listener: () => void): (() => void) => {
    this.subscribers.add(listener);
    return () => {
      this.subscribers.delete(listener);
    };
  };

  ready(): Promise<void> {
    return this.readyPromise;
  }

  getBoolean(key: string, defaultValue: boolean): boolean {
    return this.getDetail(key, defaultValue).value;
  }

  getString(key: string, defaultValue: string): string {
    return this.getDetail(key, defaultValue).value;
  }

  getNumber(key: string, defaultValue: number): number {
    return this.getDetail(key, defaultValue).value;
  }

  getJson<T>(key: string, defaultValue: T): T {
    return this.getDetail(key, defaultValue).value;
  }

  getDetail<T>(key: string, defaultValue: T): FlagDetail<T> {
    return resolveDetail(this.evaluations.get(key), defaultValue);
  }

  getAll(): Readonly<Record<string, FlagEvaluation>> {
    return Object.fromEntries(this.evaluations);
  }

  getEvaluation(key: string): FlagEvaluation | undefined {
    return this.evaluations.get(key);
  }

  async identify(context: EvaluationContext): Promise<void> {
    assertContext(context);
    this.currentContext = context;

    // Results for the previous context must never land after results for the new one.
    for (const controller of this.inflight) {
      controller.abort();
    }

    await this.refresh();
  }

  on<E extends keyof ClientEvents>(event: E, listener: ClientEvents[E]): () => void {
    return this.emitter.on(event, listener);
  }

  async close(): Promise<void> {
    if (this.closed) {
      return;
    }

    this.closed = true;
    clearTimeout(this.readyTimer);
    clearTimeout(this.debounceTimer);
    clearTimeout(this.reconnectTimer);
    this.stopPolling();
    for (const controller of this.inflight) {
      controller.abort();
    }

    this.resolveReady();
    this.setState('offline');
    const hub = this.hub;
    this.hub = undefined;
    if (hub) {
      try {
        await hub.stop();
      } catch (error) {
        this.logger.debug?.('Closing the live connection failed', error);
      }
    }

    this.emitter.clear();
    this.subscribers.clear();
  }

  private async refresh(): Promise<void> {
    if (this.closed) {
      return;
    }

    const sequence = ++this.requestSequence;
    const controller = new AbortController();
    this.inflight.add(controller);
    try {
      const response = await this.fetchImpl(`${this.baseUrl}/sdk/v1/evaluate`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${this.sdkKey}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ context: this.currentContext }),
        signal: controller.signal,
      });
      if (!response.ok) {
        throw new Error(`FlagForge evaluation failed with HTTP ${response.status}.`);
      }

      const body = (await response.json()) as EvaluateResponse;

      // A slower, older request must not overwrite newer values.
      if (this.closed || controller.signal.aborted || sequence < this.appliedSequence) {
        return;
      }

      this.appliedSequence = sequence;
      this.apply(body.flags);
      if (this.state === 'offline') {
        this.setState('polling');
      }
    } catch (error) {
      if (controller.signal.aborted || this.closed) {
        return;
      }

      this.reportError(error);
      if (this.state === 'polling') {
        this.setState('offline');
      }
    } finally {
      this.inflight.delete(controller);
    }
  }

  /** Replaces the evaluations, keeping the previous object for every flag whose evaluation did not change. */
  private apply(flags: Record<string, FlagEvaluation>): void {
    const previous = this.evaluations;
    const next = new Map<string, FlagEvaluation>();
    const changedKeys: string[] = [];
    let updated = false;
    for (const [key, incoming] of Object.entries(flags)) {
      const existing = previous.get(key);
      if (existing && deepEqual(existing, incoming)) {
        next.set(key, existing);
        continue;
      }

      next.set(key, Object.freeze({ ...incoming }));
      updated = true;
      if (!existing || !deepEqual(existing.value, incoming.value)) {
        changedKeys.push(key);
      }
    }

    for (const key of previous.keys()) {
      if (!next.has(key)) {
        changedKeys.push(key);
        updated = true;
      }
    }

    this.evaluations = next;
    const first = !this.hasEvaluated;
    if (first) {
      this.hasEvaluated = true;
      clearTimeout(this.readyTimer);
      this.resolveReady();
    }

    if (updated || first) {
      this.notify();
    }

    if (first) {
      this.emitter.emit('ready');
    }

    if (changedKeys.length > 0) {
      this.emitter.emit('change', changedKeys);
    }
  }

  private async startStreaming(): Promise<void> {
    const hub = this.hubFactory(`${this.baseUrl}/sdk/hubs/flags`, () => this.sdkKey);
    hub.on('FlagsChanged', () => this.scheduleRefresh());
    hub.onreconnecting(() => this.setState('connecting'));
    hub.onreconnected(() => {
      this.setState('live');
      void this.refresh();
    });
    hub.onclose((error) => {
      // Automatic reconnection gave up (or the server closed the connection).
      if (!this.closed) {
        this.enterPollingMode(error);
      }
    });
    this.hub = hub;
    if (!(await this.connect(reconnectDelaysMs))) {
      this.enterPollingMode(new Error('Could not open the live connection.'));
    }
  }

  /** Tries to open the live connection, waiting before each attempt. Returns whether it connected. */
  private async connect(delaysMs: readonly number[]): Promise<boolean> {
    for (const delay of delaysMs) {
      if (delay > 0) {
        await new Promise<void>((resolve) => {
          this.reconnectTimer = setTimeout(resolve, delay);
        });
      }

      const hub = this.hub;
      if (this.closed || !hub) {
        return false;
      }

      try {
        await hub.start();
        if (this.closed) {
          return false;
        }

        this.stopPolling();
        this.setState('live');

        // Catch up on anything that changed before the connection was listening.
        void this.refresh();
        return true;
      } catch (error) {
        this.logger.debug?.('Live connection attempt failed', error);
      }
    }

    return false;
  }

  private enterPollingMode(reason: Error | undefined): void {
    if (this.closed) {
      return;
    }

    if (this.state !== 'polling' && this.state !== 'offline') {
      this.logger.warn?.('Live updates are unavailable; polling for changes instead.', reason);
      this.setState('polling');
    }

    this.startPolling();
    this.scheduleBackgroundReconnect();
  }

  private scheduleBackgroundReconnect(): void {
    clearTimeout(this.reconnectTimer);
    this.reconnectTimer = setTimeout(() => {
      void this.connect([0]).then((connected) => {
        if (!connected && !this.closed) {
          this.scheduleBackgroundReconnect();
        }
      });
    }, backgroundReconnectMs);
  }

  private scheduleRefresh(): void {
    clearTimeout(this.debounceTimer);
    this.debounceTimer = setTimeout(() => {
      this.debounceTimer = undefined;
      void this.refresh();
    }, flagsChangedDebounceMs);
  }

  private startPolling(): void {
    if (this.pollTimer !== undefined || this.closed) {
      return;
    }

    this.pollTimer = setInterval(() => void this.refresh(), this.pollIntervalMs);
  }

  private stopPolling(): void {
    if (this.pollTimer !== undefined) {
      clearInterval(this.pollTimer);
      this.pollTimer = undefined;
    }
  }

  private onReadyTimeout(timeoutMs: number): void {
    this.readyTimer = undefined;
    if (this.hasEvaluated || this.closed) {
      return;
    }

    this.resolveReady();
    this.reportError(
      new Error(
        `FlagForge did not respond within ${timeoutMs} ms; serving default values until it does.`,
      ),
    );
  }

  private setState(next: ConnectionState): void {
    if (this.state === next) {
      return;
    }

    this.state = next;
    this.notify();
    this.emitter.emit('connection', next);
  }

  private notify(): void {
    for (const listener of [...this.subscribers]) {
      listener();
    }
  }

  private reportError(error: unknown): void {
    const normalized = error instanceof Error ? error : new Error(String(error));
    this.logger.warn?.(normalized.message);
    this.emitter.emit('error', normalized);
  }
}

function assertContext(context: EvaluationContext | undefined): void {
  if (
    !context ||
    typeof context.key !== 'string' ||
    context.key.length === 0 ||
    context.key.length > 256
  ) {
    throw new TypeError('FlagForge: context.key must be a string of 1 to 256 characters.');
  }
}
