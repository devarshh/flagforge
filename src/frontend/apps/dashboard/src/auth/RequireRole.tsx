import { Box, Typography } from '@mui/material';
import type { ReactNode } from 'react';
import type { Role } from '../api/types';
import { hasRole, useCurrentUser } from './authContext';

const roleNames: Record<Role, string> = { viewer: 'Viewer', editor: 'Editor', admin: 'Admin' };

export function RequireRole({ role, children }: { role: Role; children: ReactNode }) {
  const user = useCurrentUser();
  if (hasRole(user, role)) {
    return children;
  }

  return (
    <Box sx={{ py: 6 }}>
      <Typography variant="h2" component="h1">
        You do not have access to this page
      </Typography>
      <Typography color="text.secondary" sx={{ mt: 1 }}>
        This page needs the {roleNames[role]} role. Ask an admin if you need it.
      </Typography>
    </Box>
  );
}
