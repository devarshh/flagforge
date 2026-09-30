import { Box, Chip, type ChipProps } from '@mui/material';
import LockOutlined from '@mui/icons-material/LockOutlined';

export interface EnvironmentChipProps {
  name: string;
  color: string;
  isProtected?: boolean;
  size?: ChipProps['size'];
  selected?: boolean;
}

/** An environment's name with its color as a marker; the name is always shown, so color is never the only signal. */
export function EnvironmentChip({
  name,
  color,
  isProtected = false,
  size = 'small',
  selected = false,
}: EnvironmentChipProps) {
  return (
    <Chip
      size={size}
      variant={selected ? 'filled' : 'outlined'}
      label={name}
      icon={
        <Box
          component="span"
          sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5, pl: 0.75 }}
        >
          <Box
            component="span"
            aria-hidden
            sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: color }}
          />
          {isProtected && <LockOutlined aria-label="Protected" sx={{ fontSize: 14 }} />}
        </Box>
      }
      sx={{ borderColor: color, fontWeight: 500 }}
    />
  );
}
