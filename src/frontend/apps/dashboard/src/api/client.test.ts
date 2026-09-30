import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from './client';
import { ApiError } from './errors';
import type { LoginResponse, User } from './types';

const user: User = {
  id: 'u1',
  email: 'sam@flagforge.test',
  displayName: 'Sam',
  role: 'editor',
  isActive: true,
  mustChangePassword: false,
  createdAt: '2026-09-01T00:00:00Z',
  lastLoginAt: null,
  lockoutEndsAt: null,
};

function session(accessToken: string): LoginResponse {
  return { accessToken, expiresAt: '2026-09-30T12:15:00Z', user };
}

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => {
    resolve = done;
  });
  return { promise, resolve };
}

function urlOf(input: RequestInfo | URL): string {
  return typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
}

function authorization(init: RequestInit | undefined): string | undefined {
  return new Headers(init?.headers).get('Authorization') ?? undefined;
}

describe('ApiClient', () => {
  it('refreshes once for concurrent 401s and retries every request with the new token', async () => {
    const refreshResponse = deferred<Response>();
    const fetch = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = urlOf(input);
      if (url === '/api/v1/auth/refresh') {
        return refreshResponse.promise;
      }

      return Promise.resolve(
        authorization(init) === 'Bearer fresh'
          ? json({ path: url })
          : json({ title: 'Unauthorized' }, 401),
      );
    });
    const client = new ApiClient({ fetch });
    client.setSession(session('expired'));

    const settled = Promise.allSettled([
      client.get('/api/v1/projects'),
      client.get('/api/v1/audit'),
    ]);
    const authRequest = client.get('/api/v1/auth/me').catch((caught: unknown) => caught);
    await vi.waitFor(() =>
      expect(fetch).toHaveBeenCalledWith('/api/v1/auth/refresh', expect.anything()),
    );
    refreshResponse.resolve(json(session('fresh')));

    const results = await settled;
    expect(results).toEqual([
      { status: 'fulfilled', value: { path: '/api/v1/projects' } },
      { status: 'fulfilled', value: { path: '/api/v1/audit' } },
    ]);
    expect(
      fetch.mock.calls.filter(([input]) => urlOf(input) === '/api/v1/auth/refresh'),
    ).toHaveLength(1);
    const retried = fetch.mock.calls.filter(
      ([input, init]) =>
        urlOf(input) !== '/api/v1/auth/refresh' && authorization(init) === 'Bearer fresh',
    );
    expect(retried.map(([input]) => urlOf(input)).sort()).toEqual([
      '/api/v1/audit',
      '/api/v1/projects',
    ]);
    // Auth endpoints are never retried through a refresh.
    expect(await authRequest).toBeInstanceOf(ApiError);
    expect(fetch.mock.calls.filter(([input]) => urlOf(input) === '/api/v1/auth/me')).toHaveLength(
      1,
    );
  });

  it('starts a new refresh after the previous one finished', async () => {
    let tokens = 0;
    const fetch = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      if (urlOf(input) === '/api/v1/auth/refresh') {
        tokens += 1;
        return Promise.resolve(json(session(`token-${tokens}`)));
      }

      return Promise.resolve(
        authorization(init) === `Bearer token-${tokens}` && tokens > 0
          ? json({ ok: true })
          : json({}, 401),
      );
    });
    const client = new ApiClient({ fetch });

    await client.refresh();
    await client.refresh();

    expect(tokens).toBe(2);
    await expect(client.get('/api/v1/projects')).resolves.toEqual({ ok: true });
  });

  it('reports an expired session when the refresh cookie is rejected', async () => {
    const onSessionExpired = vi.fn();
    const fetch = vi.fn((input: RequestInfo | URL) =>
      Promise.resolve(
        urlOf(input) === '/api/v1/auth/refresh'
          ? json({ title: 'Unauthorized' }, 401)
          : json({ title: 'Unauthorized' }, 401),
      ),
    );
    const client = new ApiClient({ fetch, onSessionExpired });
    client.setSession(session('expired'));

    const error = await client.get('/api/v1/projects').catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(401);
    expect(onSessionExpired).toHaveBeenCalledTimes(1);
    expect(client.hasSession).toBe(false);
  });

  it('parses ProblemDetails into ApiError with field errors and extensions', async () => {
    const fetch = vi.fn(() =>
      Promise.resolve(
        json(
          {
            type: 'https://tools.ietf.org/html/rfc9110#section-15.5.10',
            title: 'Conflict',
            status: 409,
            detail: 'This flag was changed by someone else while you were editing.',
            currentVersion: 7,
            errors: { 'config.rules[0].clauses[0].values': ['Add at least one value.'] },
          },
          409,
        ),
      ),
    );
    const client = new ApiClient({ fetch });

    const error = (await client
      .put('/api/v1/x', {})
      .catch((caught: unknown) => caught)) as ApiError;

    expect(error.status).toBe(409);
    expect(error.detail).toBe('This flag was changed by someone else while you were editing.');
    expect(error.extensions.currentVersion).toBe(7);
    expect(error.fieldErrors('config.')).toEqual({
      'rules[0].clauses[0].values': ['Add at least one value.'],
    });
  });

  it('sends JSON bodies with the bearer token and returns undefined for 204', async () => {
    const fetch = vi.fn((_input: RequestInfo | URL, _init?: RequestInit) =>
      Promise.resolve(new Response(null, { status: 204 })),
    );
    const client = new ApiClient({ fetch });
    client.setSession(session('token'));

    await expect(client.delete('/api/v1/projects/p', { confirmKey: 'p' })).resolves.toBeUndefined();

    const init = fetch.mock.calls[0]?.[1];
    expect(init?.method).toBe('DELETE');
    expect(init?.body).toBe('{"confirmKey":"p"}');
    expect(authorization(init)).toBe('Bearer token');
    expect(new Headers(init?.headers).get('Content-Type')).toBe('application/json');
  });
});
