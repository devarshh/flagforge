import { Box } from '@mui/material';
import { alpha } from '@mui/material/styles';
import type { Product } from '../products';

/** A coffee bag drawn with CSS: body, folded top, round label, and a bean. No image files. */
export function CoffeeArt({ art, compact = false }: { art: Product['art']; compact?: boolean }) {
  const bagWidth = compact ? 46 : 80;
  const bagHeight = compact ? 62 : 108;
  return (
    <Box
      aria-hidden
      sx={{
        height: compact ? 96 : 168,
        width: compact ? 96 : '100%',
        flexShrink: 0,
        bgcolor: alpha(art.bag, 0.1),
        display: 'grid',
        placeItems: 'center',
        borderRadius: 'inherit',
      }}
    >
      <Box
        sx={{
          position: 'relative',
          width: bagWidth,
          height: bagHeight,
          bgcolor: art.bag,
          borderRadius: '4px 4px 10px 10px',
          boxShadow: `inset -${bagWidth / 8}px 0 0 ${alpha('#000000', 0.14)}`,
          '&::before': {
            content: '""',
            position: 'absolute',
            left: -2,
            right: -2,
            top: -bagHeight / 10,
            height: bagHeight / 7,
            bgcolor: art.bag,
            borderRadius: '3px',
            filter: 'brightness(0.8)',
          },
        }}
      >
        <Box
          sx={{
            position: 'absolute',
            top: '34%',
            left: '50%',
            transform: 'translateX(-50%)',
            width: '64%',
            aspectRatio: '1',
            borderRadius: '50%',
            bgcolor: art.label,
            display: 'grid',
            placeItems: 'center',
          }}
        >
          <Box
            sx={{
              position: 'relative',
              width: '42%',
              height: '60%',
              bgcolor: art.bean,
              borderRadius: '50%',
              transform: 'rotate(-28deg)',
              '&::after': {
                content: '""',
                position: 'absolute',
                top: '12%',
                bottom: '12%',
                left: '46%',
                width: '8%',
                bgcolor: art.label,
                borderRadius: 4,
                transform: 'rotate(10deg)',
              },
            }}
          />
        </Box>
      </Box>
    </Box>
  );
}
