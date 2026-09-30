import { Box, Button, CircularProgress, Stack, Typography } from '@mui/material';

export function FullPageLoader() {
  return (
    <Box
      sx={{ display: 'grid', placeItems: 'center', minHeight: '100vh' }}
      role="status"
      aria-label="Loading"
    >
      <CircularProgress />
    </Box>
  );
}

export function FullPageMessage({
  title,
  description,
  onRetry,
}: {
  title: string;
  description: string;
  onRetry?: () => void;
}) {
  return (
    <Box sx={{ display: 'grid', placeItems: 'center', minHeight: '100vh', px: 2 }}>
      <Stack spacing={1.5} sx={{ maxWidth: 420, textAlign: 'center', alignItems: 'center' }}>
        <Typography variant="h2" component="h1">
          {title}
        </Typography>
        <Typography color="text.secondary">{description}</Typography>
        {onRetry && (
          <Button variant="contained" onClick={onRetry}>
            Try again
          </Button>
        )}
      </Stack>
    </Box>
  );
}
