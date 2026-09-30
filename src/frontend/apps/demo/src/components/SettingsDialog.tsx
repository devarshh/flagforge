import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  Stack,
  TextField,
} from '@mui/material';
import { useState } from 'react';
import { isSdkKey } from '../sdkKey';

export interface SettingsDialogProps {
  open: boolean;
  currentKey: string | null;
  /** True when a pasted key overrides the deployment's key, so it can be cleared. */
  usingStoredKey: boolean;
  onSave: (key: string) => void;
  onUseDefault: () => void;
  onClose: () => void;
}

export function SettingsDialog(props: SettingsDialogProps) {
  // Remounting on open starts from the key in use.
  return props.open ? <SettingsDialogContent {...props} /> : null;
}

function SettingsDialogContent({
  open,
  currentKey,
  usingStoredKey,
  onSave,
  onUseDefault,
  onClose,
}: SettingsDialogProps) {
  const [value, setValue] = useState(currentKey ?? '');
  const [submitted, setSubmitted] = useState(false);
  const trimmed = value.trim();
  const valid = isSdkKey(trimmed);
  return (
    <Dialog
      open={open}
      onClose={currentKey ? onClose : undefined}
      maxWidth="sm"
      fullWidth
      aria-labelledby="settings-title"
    >
      <form
        noValidate
        onSubmit={(event) => {
          event.preventDefault();
          setSubmitted(true);
          if (valid) {
            onSave(trimmed);
          }
        }}
      >
        <DialogTitle id="settings-title">Connect to FlagForge</DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            {!currentKey && (
              <Alert severity="info">This store needs an SDK key to read its flags.</Alert>
            )}
            <DialogContentText>
              Paste an SDK key from Project settings in the FlagForge dashboard. It identifies one
              environment, such as development. The key is saved in this browser only.
            </DialogContentText>
            <TextField
              label="SDK key"
              value={value}
              onChange={(event) => setValue(event.target.value)}
              autoFocus
              fullWidth
              error={submitted && !valid}
              helperText={
                submitted && !valid
                  ? 'SDK keys start with ffk_ and have no spaces.'
                  : 'Starts with ffk_'
              }
              slotProps={{
                htmlInput: { className: 'mono', spellCheck: false, autoComplete: 'off' },
              }}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          {usingStoredKey && <Button onClick={onUseDefault}>Use the default key</Button>}
          {currentKey && <Button onClick={onClose}>Cancel</Button>}
          <Button type="submit" variant="contained">
            Connect
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
