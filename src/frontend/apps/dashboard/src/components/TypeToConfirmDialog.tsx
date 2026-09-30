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
import { useState, type ReactNode } from 'react';

export interface TypeToConfirmDialogProps {
  open: boolean;
  title: string;
  description: ReactNode;
  /** The text the person must type, usually a key. */
  confirmText: string;
  confirmLabel: string;
  destructive?: boolean;
  /** Ask for a comment (recorded in the audit log); required when true. */
  requireComment?: boolean;
  pending?: boolean;
  error?: string | null;
  onConfirm: (comment: string) => void;
  onClose: () => void;
}

/** Confirms risky actions by making the person type the resource key (and, for protected changes, a reason). */
export function TypeToConfirmDialog(props: TypeToConfirmDialogProps) {
  // Remounting on open resets the typed text and comment.
  return props.open ? <TypeToConfirmDialogContent {...props} /> : null;
}

function TypeToConfirmDialogContent({
  open,
  title,
  description,
  confirmText,
  confirmLabel,
  destructive = false,
  requireComment = false,
  pending = false,
  error,
  onConfirm,
  onClose,
}: TypeToConfirmDialogProps) {
  const [typed, setTyped] = useState('');
  const [comment, setComment] = useState('');
  const canConfirm = typed === confirmText && (!requireComment || comment.trim().length > 0);
  return (
    <Dialog
      open={open}
      onClose={pending ? undefined : onClose}
      aria-labelledby="type-confirm-title"
      maxWidth="sm"
      fullWidth
    >
      <form
        onSubmit={(event) => {
          event.preventDefault();
          if (canConfirm) {
            onConfirm(comment.trim());
          }
        }}
      >
        <DialogTitle id="type-confirm-title">{title}</DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            <DialogContentText component="div">{description}</DialogContentText>
            {error && <Alert severity="error">{error}</Alert>}
            <TextField
              label={`Type ${confirmText} to confirm`}
              value={typed}
              onChange={(event) => setTyped(event.target.value)}
              autoFocus
              autoComplete="off"
              slotProps={{ htmlInput: { className: 'mono', spellCheck: false } }}
            />
            {requireComment && (
              <TextField
                label="Comment (required)"
                helperText="Recorded in the audit log with this change."
                value={comment}
                onChange={(event) => setComment(event.target.value)}
                multiline
                minRows={2}
              />
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose} disabled={pending}>
            Cancel
          </Button>
          <Button
            type="submit"
            variant="contained"
            color={destructive ? 'error' : 'primary'}
            disabled={!canConfirm}
            loading={pending}
          >
            {confirmLabel}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
