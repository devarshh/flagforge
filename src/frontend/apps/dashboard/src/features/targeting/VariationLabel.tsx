import { Box, Typography } from '@mui/material';
import type { Variation } from '../../api/types';
import { variationColor } from '../../theme/tokens';
import { displayValue } from '../flags/variationValues';

/** A variation's color marker, name, and value (in the code font). */
export function VariationLabel({ variation, index }: { variation: Variation; index: number }) {
  return (
    <Box
      component="span"
      sx={{ display: 'inline-flex', alignItems: 'center', gap: 1, minWidth: 0, maxWidth: '100%' }}
    >
      <Box
        component="span"
        aria-hidden
        sx={{
          width: 10,
          height: 10,
          borderRadius: '2px',
          flexShrink: 0,
          bgcolor: variationColor(index),
        }}
      />
      <Box component="span" sx={{ flexShrink: 0 }}>
        {variation.name}
      </Box>
      <Typography
        component="span"
        variant="body2"
        color="text.secondary"
        className="mono"
        noWrap
        sx={{ minWidth: 0 }}
      >
        {displayValue(variation.value)}
      </Typography>
    </Box>
  );
}
