import { Box, Button, CircularProgress, Stack, Typography } from '@mui/material';

/** Shown until the store has an SDK key: briefly while it loads the configured key, or until someone pastes one. */
export function Welcome({
  resolving,
  onOpenSettings,
}: {
  resolving: boolean;
  onOpenSettings: () => void;
}) {
  return (
    <Box
      component="main"
      sx={{
        minHeight: '100vh',
        display: 'grid',
        placeItems: 'center',
        px: 2,
        bgcolor: 'background.default',
      }}
    >
      <Stack spacing={2} sx={{ alignItems: 'center', textAlign: 'center', maxWidth: 420 }}>
        <Typography variant="h1">Acme Coffee</Typography>
        {resolving ? (
          <CircularProgress aria-label="Loading the store" />
        ) : (
          <>
            <Typography color="text.secondary">
              Connect the store to FlagForge to load its flags.
            </Typography>
            <Button variant="contained" onClick={onOpenSettings}>
              Enter an SDK key
            </Button>
          </>
        )}
      </Stack>
    </Box>
  );
}
