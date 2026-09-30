import { toApiError } from './errors';
import type { LoginResponse } from './types';

export interface ApiClientOptions {
  fetch?: typeof fetch;
  /** Called when the session cannot be refreshed; the app signs the user out. */
  onSessionExpired?: () => void;
}

type Method = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';

/**
 * The one HTTP client. The access token lives only in memory. On a 401 the client refreshes once (using the
 * httpOnly refresh cookie) and retries; concurrent 401s share a single in-flight refresh, which matters because
 * refresh tokens rotate and a second refresh with the same cookie would be treated as token reuse.
 */
export class ApiClient {
  private accessToken: string | null = null;
  private refreshInFlight: Promise<LoginResponse | null> | null = null;
  private readonly fetchImpl: typeof fetch;

  constructor(private readonly options: ApiClientOptions = {}) {
    this.fetchImpl = options.fetch ?? ((input, init) => globalThis.fetch(input, init));
  }

  setSessionExpiredHandler(callback: (() => void) | undefined): void {
    this.options.onSessionExpired = callback;
  }

  setSession(session: LoginResponse | null): void {
    this.accessToken = session?.accessToken ?? null;
  }

  get hasSession(): boolean {
    return this.accessToken !== null;
  }

  /** Exchanges the refresh cookie for a new access token. Concurrent callers share one request. */
  refresh(): Promise<LoginResponse | null> {
    this.refreshInFlight ??= this.performRefresh().finally(() => {
      this.refreshInFlight = null;
    });
    return this.refreshInFlight;
  }

  get<T>(path: string): Promise<T> {
    return this.request<T>('GET', path);
  }

  post<T>(path: string, body?: unknown): Promise<T> {
    return this.request<T>('POST', path, body);
  }

  put<T>(path: string, body: unknown): Promise<T> {
    return this.request<T>('PUT', path, body);
  }

  patch<T>(path: string, body: unknown): Promise<T> {
    return this.request<T>('PATCH', path, body);
  }

  delete<T = void>(path: string, body?: unknown): Promise<T> {
    return this.request<T>('DELETE', path, body);
  }

  async request<T>(method: Method, path: string, body?: unknown): Promise<T> {
    let response = await this.send(method, path, body);
    if (response.status === 401 && !path.startsWith('/api/v1/auth/')) {
      const session = await this.refresh();
      if (!session) {
        this.options.onSessionExpired?.();
        throw await toApiError(response);
      }

      response = await this.send(method, path, body);
    }

    if (!response.ok) {
      throw await toApiError(response);
    }

    if (response.status === 204) {
      return undefined as T;
    }

    return (await response.json()) as T;
  }

  private send(method: Method, path: string, body: unknown): Promise<Response> {
    const headers: Record<string, string> = { Accept: 'application/json' };
    if (this.accessToken) {
      headers.Authorization = `Bearer ${this.accessToken}`;
    }

    if (body !== undefined) {
      headers['Content-Type'] = 'application/json';
    }

    return this.fetchImpl(path, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      credentials: 'same-origin',
    });
  }

  /** Resolves null when the server rejects the cookie; network failures reject so callers can tell them apart. */
  private async performRefresh(): Promise<LoginResponse | null> {
    const response = await this.fetchImpl('/api/v1/auth/refresh', {
      method: 'POST',
      headers: { Accept: 'application/json' },
      credentials: 'same-origin',
    });
    if (!response.ok) {
      this.accessToken = null;
      return null;
    }

    const session = (await response.json()) as LoginResponse;
    this.accessToken = session.accessToken;
    return session;
  }
}

export const apiClient = new ApiClient();
