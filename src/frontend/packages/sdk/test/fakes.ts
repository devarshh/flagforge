import type { HubConnectionFactory, HubConnectionLike, Logger } from '../src/index.js';

export const silentLogger: Logger = {};

export interface FetchCall {
  readonly url: string;
  readonly context: unknown;
  readonly headers: Record<string, string>;
  readonly signal: AbortSignal | undefined;
  respond(flags: Record<string, unknown>, environmentVersion?: number): void;
  fail(status?: number): void;
}

/** A fetch whose responses the test resolves explicitly, in any order. */
export function createFakeFetch() {
  const calls: FetchCall[] = [];
  const fetch: typeof globalThis.fetch = (input, init) =>
    new Promise<Response>((resolve, reject) => {
      const body = JSON.parse(typeof init?.body === 'string' ? init.body : '{}') as {
        context: unknown;
      };
      const signal = init?.signal ?? undefined;
      signal?.addEventListener('abort', () =>
        reject(new DOMException('The operation was aborted.', 'AbortError')),
      );
      calls.push({
        url: typeof input === 'string' ? input : input instanceof URL ? input.href : input.url,
        context: body.context,
        headers: init?.headers as Record<string, string>,
        signal,
        respond: (flags, environmentVersion = 1) =>
          resolve(
            new Response(JSON.stringify({ environmentVersion, flags }), {
              status: 200,
              headers: { 'Content-Type': 'application/json' },
            }),
          ),
        fail: (status = 500) => resolve(new Response('{}', { status })),
      });
    });
  return { fetch, calls };
}

/** A served flag as the evaluation API returns it. */
export function served(value: unknown, variationId = 'v', kind = 'FALLTHROUGH') {
  return { value, variationId, reason: { kind, inRollout: false } };
}

export class FakeHub implements HubConnectionLike {
  starts = 0;
  stops = 0;

  /** How many upcoming start() calls fail. */
  failNextStarts = 0;

  private readonly handlers = new Map<string, ((...args: unknown[]) => void)[]>();
  private readonly closeCallbacks: ((error?: Error) => void)[] = [];
  private readonly reconnectingCallbacks: ((error?: Error) => void)[] = [];
  private readonly reconnectedCallbacks: ((connectionId?: string) => void)[] = [];

  constructor(
    readonly url: string,
    readonly accessTokenFactory: () => string,
  ) {}

  start(): Promise<void> {
    this.starts++;
    if (this.failNextStarts > 0) {
      this.failNextStarts--;
      return Promise.reject(new Error('connect ECONNREFUSED'));
    }

    return Promise.resolve();
  }

  stop(): Promise<void> {
    this.stops++;
    return Promise.resolve();
  }

  on(methodName: string, handler: (...args: unknown[]) => void): void {
    this.handlers.set(methodName, [...(this.handlers.get(methodName) ?? []), handler]);
  }

  onclose(callback: (error?: Error) => void): void {
    this.closeCallbacks.push(callback);
  }

  onreconnecting(callback: (error?: Error) => void): void {
    this.reconnectingCallbacks.push(callback);
  }

  onreconnected(callback: (connectionId?: string) => void): void {
    this.reconnectedCallbacks.push(callback);
  }

  /** Delivers a server-to-client message. */
  send(methodName: string, ...args: unknown[]): void {
    for (const handler of this.handlers.get(methodName) ?? []) {
      handler(...args);
    }
  }

  simulateReconnecting(): void {
    for (const callback of this.reconnectingCallbacks) {
      callback(new Error('connection lost'));
    }
  }

  simulateReconnected(): void {
    for (const callback of this.reconnectedCallbacks) {
      callback('reconnected-id');
    }
  }

  simulateClose(): void {
    for (const callback of this.closeCallbacks) {
      callback(new Error('gave up reconnecting'));
    }
  }
}

export function createFakeHubFactory(configure?: (hub: FakeHub) => void) {
  const hubs: FakeHub[] = [];
  const factory: HubConnectionFactory = (url, accessTokenFactory) => {
    const hub = new FakeHub(url, accessTokenFactory);
    configure?.(hub);
    hubs.push(hub);
    return hub;
  };
  return { factory, hubs };
}
