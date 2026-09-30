import {
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
} from '@mui/material';
import { useState } from 'react';

export interface ConflictDialogProps {
  open: boolean;
  /** The draft as JSON, offered for copying so nothing is lost. */
  draftJson: string;
  onLoadLatest: () => void;
  onClose: () => void;
}

/** Shown when a save returns 409: someone else saved first. */
export function ConflictDialog({ open, draftJson, onLoadLatest, onClose }: ConflictDialogProps) {
  const [copied, setCopied] = useState(false);
  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth="sm"
      fullWidth
      aria-labelledby="conflict-title"
      slotProps={{ transition: { onExited: () => setCopied(false) } }}
    >
      <DialogTitle id="conflict-title">Someone else changed this flag</DialogTitle>
      <DialogContent>
        <DialogContentText>
          This flag was changed by someone else while you were editing. Load the latest version to
          continue from their changes (this discards your draft), or copy your draft as JSON first
          to keep it.
        </DialogContentText>
      </DialogContent>
      <DialogActions>
        <Button
          onClick={() => void navigator.clipboard.writeText(draftJson).then(() => setCopied(true))}
        >
          {copied ? 'Draft copied' : 'Copy my draft as JSON'}
        </Button>
        <Button variant="contained" onClick={onLoadLatest}>
          Load latest version
        </Button>
      </DialogActions>
    </Dialog>
  );
}
