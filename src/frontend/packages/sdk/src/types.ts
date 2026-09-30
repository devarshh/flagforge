/** A context attribute value: a string, number, or boolean, or an array of those. */
export type AttributeValue = string | number | boolean | ReadonlyArray<string | number | boolean>;

/** The subject of an evaluation. `key` identifies it (for example a user id) and drives percentage rollouts. */
export interface EvaluationContext {
  readonly key: string;
  readonly attributes?: Readonly<Record<string, AttributeValue | null>>;
}

export type JsonValue =
  string | number | boolean | null | JsonValue[] | { [key: string]: JsonValue };

export type ReasonKind =
  'OFF' | 'TARGET_MATCH' | 'RULE_MATCH' | 'FALLTHROUGH' | 'FLAG_NOT_FOUND' | 'ERROR';

/** Why a value was served. `ruleIndex` is zero-based. */
export interface EvaluationReason {
  readonly kind: ReasonKind;
  readonly ruleId?: string;
  readonly ruleIndex?: number;
  readonly inRollout?: boolean;
}

/** One flag as served by FlagForge. */
export interface FlagEvaluation {
  readonly value: JsonValue;
  readonly variationId?: string;
  readonly reason: EvaluationReason;
}

/** A typed result: the served value, or the caller's default when the flag is unavailable or has the wrong type. */
export interface FlagDetail<T> {
  readonly value: T;
  readonly variationId?: string;
  readonly reason: EvaluationReason;
}

/** `connecting` while opening (or reopening) the live connection, `live` when streaming, `polling` when streaming
 * is unavailable or disabled, `offline` after `close()` or while polling fails. */
export type ConnectionState = 'connecting' | 'live' | 'polling' | 'offline';

export interface Logger {
  debug?(message: string, ...details: unknown[]): void;
  warn?(message: string, ...details: unknown[]): void;
  error?(message: string, ...details: unknown[]): void;
}

/** The subset of a SignalR `HubConnection` the client uses; injectable for tests. */
export interface HubConnectionLike {
  start(): Promise<void>;
  stop(): Promise<void>;
  on(methodName: string, handler: (...args: unknown[]) => void): void;
  onclose(callback: (error?: Error) => void): void;
  onreconnecting(callback: (error?: Error) => void): void;
  onreconnected(callback: (connectionId?: string) => void): void;
}

export type HubConnectionFactory = (
  url: string,
  accessTokenFactory: () => string,
) => HubConnectionLike;

export interface ClientOptions {
  /** Origin that serves `/sdk`, for example `https://flags.example.com` or `window.location.origin`. */
  readonly baseUrl: string;
  /** An SDK key (`ffk_...`). SDK keys identify an environment; they are public, not secrets. */
  readonly sdkKey: string;
  readonly context: EvaluationContext;
  /** Receive changes over a WebSocket. Default `true`. */
  readonly streaming?: boolean;
  /** Poll interval when streaming is unavailable or disabled. Default 30 000 ms. */
  readonly pollIntervalMs?: number;
  /** `ready()` resolves after this long even without a response. Default 5 000 ms. */
  readonly readyTimeoutMs?: number;
  readonly logger?: Logger;
  /** Replaces the global `fetch` (tests, custom transports). */
  readonly fetch?: typeof fetch;
  /** Replaces the SignalR connection (tests). */
  readonly hubConnectionFactory?: HubConnectionFactory;
}

export interface ClientEvents {
  /** Keys whose values changed (deep comparison for JSON), including flags that disappeared. */
  change: (changedKeys: string[]) => void;
  ready: () => void;
  error: (error: Error) => void;
  connection: (state: ConnectionState) => void;
}

export interface FlagForgeClient {
  /** Resolves after the first successful evaluation, or after `readyTimeoutMs`. Never rejects. */
  ready(): Promise<void>;
  readonly isReady: boolean;
  readonly connectionState: ConnectionState;
  readonly context: EvaluationContext;
  getBoolean(key: string, defaultValue: boolean): boolean;
  getString(key: string, defaultValue: string): string;
  getNumber(key: string, defaultValue: number): number;
  getJson<T>(key: string, defaultValue: T): T;
  getDetail<T>(key: string, defaultValue: T): FlagDetail<T>;
  /** Every flag as last served. */
  getAll(): Readonly<Record<string, FlagEvaluation>>;
  /** The raw evaluation of one flag. The object is replaced only when the flag's evaluation changes. */
  getEvaluation(key: string): FlagEvaluation | undefined;
  /** Switches to another context (for example after sign-in) and re-evaluates. */
  identify(context: EvaluationContext): Promise<void>;
  on<E extends keyof ClientEvents>(event: E, listener: ClientEvents[E]): () => void;
  /** Notifies on any state change (evaluations, readiness, connection); built for `useSyncExternalStore`. */
  readonly subscribe: (listener: () => void) => () => void;
  close(): Promise<void>;
}
