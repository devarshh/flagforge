import { createContext, useContext } from 'react';
import type { Role, User } from '../api/types';

export type AuthState =
  | { status: 'loading' }
  | { status: 'signedOut' }
  | { status: 'unreachable' }
  | { status: 'signedIn'; user: User };

export interface AuthContextValue {
  state: AuthState;
  login: (email: string, password: string) => Promise<User>;
  logout: () => Promise<void>;
  setUser: (user: User) => void;
  retry: () => void;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const auth = useContext(AuthContext);
  if (!auth) {
    throw new Error('useAuth must be used inside <AuthProvider>.');
  }

  return auth;
}

/** The signed-in user; only call below <RequireAuth>. */
export function useCurrentUser(): User {
  const { state } = useAuth();
  if (state.status !== 'signedIn') {
    throw new Error('useCurrentUser needs a signed-in user.');
  }

  return state.user;
}

const rank: Record<Role, number> = { viewer: 0, editor: 1, admin: 2 };

export function hasRole(user: Pick<User, 'role'>, minimum: Role): boolean {
  return rank[user.role] >= rank[minimum];
}
