import { Box, Button, Stack, Typography } from '@mui/material';
import { isRouteErrorResponse, useRouteError } from 'react-router';
import { errorMessage } from '../api/errors';

/** Shown inside the app shell when a page fails to render or load, so navigation keeps working. */
export function RouteError() {
  const error = useRouteError();
  const notFound = isRouteErrorResponse(error) && error.status === 404;
  return (
    <Box sx={{ py: 6 }} role="alert">
      <Stack spacing={1.5} sx={{ maxWidth: 560 }}>
        <Typography variant="h2" component="h1">
          {notFound ? 'Page not found' : 'This page could not be shown'}
        </Typography>
        <Typography color="text.secondary">
          {notFound ? 'Check the address, or go back to your projects.' : errorMessage(error)}
        </Typography>
        <Box>
          <Button variant="contained" onClick={() => window.location.reload()}>
            Reload the page
          </Button>
        </Box>
      </Stack>
    </Box>
  );
}

export function NotFoundPage() {
  return (
    <Box sx={{ py: 6 }}>
      <Typography variant="h2" component="h1">
        Page not found
      </Typography>
      <Typography color="text.secondary" sx={{ mt: 1 }}>
        Check the address, or go back to your projects.
      </Typography>
    </Box>
  );
}

export function PageSkeleton() {
  return <Box sx={{ minHeight: '50vh' }} role="status" aria-label="Loading" />;
}
