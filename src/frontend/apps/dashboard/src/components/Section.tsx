import { Box, Chip, Paper, Stack, Typography } from '@mui/material';
import type { ReactNode } from 'react';

export interface SectionProps {
  title: string;
  description?: ReactNode;
  actions?: ReactNode;
  /** Outlines the section, for example when a test evaluation landed here. */
  highlighted?: boolean;
  highlightLabel?: string;
  children?: ReactNode;
}

/** A titled panel: flat, 1 px border, 8 px radius. */
export function Section({
  title,
  description,
  actions,
  highlighted = false,
  highlightLabel,
  children,
}: SectionProps) {
  return (
    <Paper
      variant="outlined"
      component="section"
      aria-label={title}
      sx={{
        p: { xs: 2, sm: 2.5 },
        borderRadius: 2,
        borderWidth: highlighted ? 2 : 1,
        borderColor: highlighted ? 'primary.main' : 'divider',
      }}
    >
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        spacing={1.5}
        sx={{ alignItems: 'flex-start', mb: children ? 2 : 0 }}
      >
        <Box sx={{ flexGrow: 1, minWidth: 0 }}>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            <Typography variant="h3" component="h2">
              {title}
            </Typography>
            {highlighted && highlightLabel && (
              <Chip size="small" color="primary" label={highlightLabel} />
            )}
          </Stack>
          {description && (
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
              {description}
            </Typography>
          )}
        </Box>
        {actions && <Box sx={{ flexShrink: 0 }}>{actions}</Box>}
      </Stack>
      {children}
    </Paper>
  );
}
