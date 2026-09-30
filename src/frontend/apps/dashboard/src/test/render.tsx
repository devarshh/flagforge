import { ThemeProvider } from '@mui/material/styles';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, type RenderResult } from '@testing-library/react';
import type { ReactElement, ReactNode } from 'react';
import { vi } from 'vitest';
import type { Role, User } from '../api/types';
import { AuthContext, type AuthContextValue } from '../auth/authContext';
import { NotificationsProvider } from '../components/NotificationsProvider';
import { theme } from '../theme/theme';

const userIds: Record<Role, string> = {
  viewer: '00000000-0000-7000-8000-000000000001',
  editor: '00000000-0000-7000-8000-000000000002',
  admin: '00000000-0000-7000-8000-000000000003',
};

export function testUser(role: Role, overrides: Partial<User> = {}): User {
  return {
    id: userIds[role],
    email: `${role}@flagforge.test`,
    displayName: `Test ${role}`,
    role,
    isActive: true,
    mustChangePassword: false,
    createdAt: '2026-09-01T00:00:00Z',
    lastLoginAt: null,
    lockoutEndsAt: null,
    ...overrides,
  };
}

export function createTestQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity }, mutations: { retry: false } },
  });
}

export interface ProvidersOptions {
  user?: User;
  queryClient?: QueryClient;
}

/** The app's providers (theme, queries, notifications, a signed-in user) around a component under test. */
export function renderWithProviders(
  ui: ReactElement,
  { user = testUser('admin'), queryClient = createTestQueryClient() }: ProvidersOptions = {},
): RenderResult & { queryClient: QueryClient } {
  const auth: AuthContextValue = {
    state: { status: 'signedIn', user },
    login: vi.fn(),
    logout: vi.fn(),
    setUser: vi.fn(),
    retry: vi.fn(),
  };
  function Providers({ children }: { children?: ReactNode }) {
    return (
      <ThemeProvider theme={theme}>
        <QueryClientProvider client={queryClient}>
          <NotificationsProvider>
            <AuthContext.Provider value={auth}>{children}</AuthContext.Provider>
          </NotificationsProvider>
        </QueryClientProvider>
      </ThemeProvider>
    );
  }

  return Object.assign(render(ui, { wrapper: Providers }), { queryClient });
}

export function jsonResponse(body: unknown, status = 200): Response {
  return new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': status === 204 ? 'text/plain' : 'application/json' },
  });
}

export interface RecordedRequest {
  url: string;
  method: string;
  headers: Record<string, string>;
  body: unknown;
}

/** Replaces the global fetch with a handler and records every request (method, URL, headers, JSON body). */
export function stubFetch(handler: (request: RecordedRequest) => Response | Promise<Response>) {
  const requests: RecordedRequest[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init: RequestInit = {}) => {
    const request: RecordedRequest = {
      url: typeof input === 'string' ? input : input instanceof URL ? input.href : input.url,
      method: init.method ?? 'GET',
      headers: Object.fromEntries(new Headers(init.headers).entries()),
      body: typeof init.body === 'string' ? (JSON.parse(init.body) as unknown) : undefined,
    };
    requests.push(request);
    return handler(request);
  });
  vi.stubGlobal('fetch', fetchMock);
  return { requests, fetchMock };
}
