import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router';
import { useAuth } from './authContext';
import { FullPageLoader, FullPageMessage } from './FullPageMessage';

/** Sends signed-out people to the login page, and people with a temporary password to change it first. */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { state, retry } = useAuth();
  const location = useLocation();
  switch (state.status) {
    case 'loading':
      return <FullPageLoader />;
    case 'unreachable':
      return (
        <FullPageMessage
          title="FlagForge is not reachable"
          description="The dashboard could not contact the management API. Check that it is running, then try again."
          onRetry={retry}
        />
      );
    case 'signedOut':
      return (
        <Navigate to="/login" replace state={{ from: `${location.pathname}${location.search}` }} />
      );
    case 'signedIn':
      if (state.user.mustChangePassword && location.pathname !== '/account/password') {
        return <Navigate to="/account/password" replace />;
      }

      return children;
  }
}
