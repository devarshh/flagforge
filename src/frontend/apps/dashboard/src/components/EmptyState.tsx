import { Box, Typography } from '@mui/material';
import type { ReactNode } from 'react';

export interface EmptyStateProps {
  title: string;
  description?: string;
  action?: ReactNode;
}

/** An empty list that invites the next action. */
export function EmptyState({ title, description, action }: EmptyStateProps) {
  return (
    <Box
      sx={{
        py: 6,
        px: 3,
        textAlign: 'center',
        border: 1,
        borderColor: 'divider',
        borderStyle: 'dashed',
        borderRadius: 2,
      }}
    >
      <Typography variant="h4" component="p">
        {title}
      </Typography>
      {description && (
        <Typography color="text.secondary" sx={{ mt: 1, maxWidth: 480, mx: 'auto' }}>
          {description}
        </Typography>
      )}
      {action && <Box sx={{ mt: 2.5 }}>{action}</Box>}
    </Box>
  );
}
