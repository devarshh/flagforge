import { Box, Typography } from '@mui/material';
import { useMemo } from 'react';
import { tint } from '../theme/tint';
import { diffLines, formatJson } from './diff';

export interface JsonDiffProps {
  before: unknown;
  after: unknown;
}

/** A unified line diff of two JSON values; added lines are green, removed lines red, each with a +/- marker. */
export function JsonDiff({ before, after }: JsonDiffProps) {
  const lines = useMemo(() => diffLines(formatJson(before), formatJson(after)), [before, after]);
  if (lines.length === 0) {
    return <Typography color="text.secondary">Nothing recorded.</Typography>;
  }

  if (lines.every((line) => line.kind === 'same')) {
    return <Typography color="text.secondary">No differences.</Typography>;
  }

  return (
    <Box
      component="pre"
      className="mono"
      sx={{
        m: 0,
        p: 1,
        fontSize: 13,
        lineHeight: 1.6,
        overflow: 'auto',
        maxHeight: 420,
        border: 1,
        borderColor: 'divider',
        borderRadius: 1,
        bgcolor: 'background.default',
      }}
    >
      {lines.map((line, index) => (
        <Box
          component="div"
          key={index}
          sx={(theme) => ({
            px: 1,
            whiteSpace: 'pre',
            bgcolor:
              line.kind === 'added'
                ? tint(theme, 'success', 0.14)
                : line.kind === 'removed'
                  ? tint(theme, 'error', 0.12)
                  : 'transparent',
          })}
        >
          <Box
            component="span"
            aria-hidden
            sx={{ display: 'inline-block', width: 16, color: 'text.secondary' }}
          >
            {line.kind === 'added' ? '+' : line.kind === 'removed' ? '-' : ' '}
          </Box>
          <span className="visually-hidden">
            {line.kind === 'added' ? 'Added: ' : line.kind === 'removed' ? 'Removed: ' : ''}
          </span>
          {line.text}
        </Box>
      ))}
    </Box>
  );
}
