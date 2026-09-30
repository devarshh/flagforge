import { Chip } from '@mui/material';
import { tint } from '../theme/tint';

export type Tone = 'success' | 'warning' | 'error' | 'primary' | 'neutral';

/** A small status chip: tinted background with readable text (AA contrast) in both color modes. */
export function ToneChip({ label, tone }: { label: string; tone: Tone }) {
  return (
    <Chip
      size="small"
      label={label}
      sx={(theme) =>
        tone === 'neutral'
          ? { bgcolor: 'action.selected', color: 'text.secondary', fontWeight: 500 }
          : {
              bgcolor: tint(theme, tone, 0.14),
              color: tone === 'primary' ? 'primary.main' : `${tone}.dark`,
              fontWeight: 500,
            }
      }
    />
  );
}
