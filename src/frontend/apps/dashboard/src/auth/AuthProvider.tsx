import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { apiClient, type ApiClient } from '../api/client';
import type { LoginResponse, User } from '../api/types';
import { AuthContext, type AuthContextValue, type AuthState } from './authContext';

/**
 * Restores the session on startup by exchanging the refresh cookie for an access token (kept only in memory), and
 * signs out when a refresh fails later.
 */
export function AuthProvider({
  children,
  client = apiClient,
}: {
  children?: ReactNode;
  client?: ApiClient;
}) {
  const queryClient = useQueryClient();
  const [state, setState] = useState<AuthState>({ status: 'loading' });
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let cancelled = false;
    client.setSessionExpiredHandler(() => {
      queryClient.clear();
      setState({ status: 'signedOut' });
    });
    client.refresh().then(
      (session) => {
        if (!cancelled) {
          setState(session ? { status: 'signedIn', user: session.user } : { status: 'signedOut' });
        }
      },
      () => {
        if (!cancelled) {
          setState({ status: 'unreachable' });
        }
      },
    );
    return () => {
      cancelled = true;
      client.setSessionExpiredHandler(undefined);
    };
  }, [client, queryClient, attempt]);

  const login = useCallback(
    async (email: string, password: string) => {
      const session = await client.post<LoginResponse>('/api/v1/auth/login', { email, password });
      client.setSession(session);
      setState({ status: 'signedIn', user: session.user });
      return session.user;
    },
    [client],
  );

  const logout = useCallback(async () => {
    try {
      await client.post('/api/v1/auth/logout');
    } finally {
      client.setSession(null);
      queryClient.clear();
      setState({ status: 'signedOut' });
    }
  }, [client, queryClient]);

  const setUser = useCallback((user: User) => setState({ status: 'signedIn', user }), []);
  const retry = useCallback(() => {
    setState({ status: 'loading' });
    setAttempt((value) => value + 1);
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({ state, login, logout, setUser, retry }),
    [state, login, logout, setUser, retry],
  );
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
