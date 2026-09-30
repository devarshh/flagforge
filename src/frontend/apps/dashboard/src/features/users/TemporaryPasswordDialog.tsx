import {
  Alert,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  Stack,
  Typography,
} from '@mui/material';
import { CopyButton } from '../../components/CopyButton';

export interface TemporaryPasswordDialogProps {
  /** Who the password is for; null closes the dialog. */
  person: { displayName: string; email: string } | null;
  password: string;
  onClose: () => void;
}

/** Shows a temporary password once. FlagForge does not send email, so the admin shares it. */
export function TemporaryPasswordDialog({
  person,
  password,
  onClose,
}: TemporaryPasswordDialogProps) {
  return (
    <Dialog
      open={person !== null}
      onClose={onClose}
      maxWidth="sm"
      fullWidth
      aria-labelledby="temporary-password-title"
    >
      <DialogTitle id="temporary-password-title">
        Temporary password for {person?.displayName}
      </DialogTitle>
      <DialogContent>
        <Stack spacing={2}>
          <Alert severity="warning">Copy this password now. It is shown only once.</Alert>
          <Box
            sx={{
              display: 'flex',
              alignItems: 'center',
              gap: 1,
              p: 1.5,
              border: 1,
              borderColor: 'divider',
              borderRadius: 1,
              bgcolor: 'background.default',
            }}
          >
            <Typography
              className="mono"
              sx={{ flexGrow: 1, wordBreak: 'break-all' }}
              aria-label="Temporary password"
            >
              {password}
            </Typography>
            <CopyButton value={password} label="Copy temporary password" />
          </Box>
          <DialogContentText>
            Share it with {person?.email} over a channel you trust. They must choose their own
            password when they sign in.
          </DialogContentText>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button variant="contained" onClick={onClose}>
          Done
        </Button>
      </DialogActions>
    </Dialog>
  );
}
