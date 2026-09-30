import { Box, Typography } from '@mui/material';
import { useFlag } from '@flagforge/sdk/react';
import { flagDefaults, flagKeys } from '../flags';

export function PromoBanner() {
  const show = useFlag(flagKeys.promoBanner, flagDefaults.promoBanner);
  const text = useFlag(flagKeys.promoBannerText, flagDefaults.promoBannerText);
  if (!show) {
    return null;
  }

  return (
    <Box
      role="region"
      aria-label="Promotion"
      sx={{
        bgcolor: 'secondary.main',
        color: 'secondary.contrastText',
        py: 1.25,
        px: 2,
        textAlign: 'center',
      }}
    >
      <Typography sx={{ fontWeight: 600 }}>{text}</Typography>
    </Box>
  );
}
