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
import type { Environment, TargetingConfig } from '../../api/types';
import { JsonDiff } from '../../components/JsonDiff';

export interface ReviewChangesDialogProps {
  open: boolean;
  environment: Pick<Environment, 'name' | 'isProtected'>;
  saved: TargetingConfig;
  draft: TargetingConfig;
  pending: boolean;
  error: string | null;
  onSave: (comment: string) => void;
  onClose: () => void;
}

/** Shows the saved config against the draft; protected environments need a comment, which the audit log keeps. */
export function ReviewChangesDialog(props: ReviewChangesDialogProps) {
  // Remounting on open clears the previous comment.
  return props.open ? <ReviewChangesDialogContent {...props} /> : null;
}

function ReviewChangesDialogContent({
  open,
  environment,
  saved,
  draft,
  pending,
  error,
  onSave,
  onClose,
}: ReviewChangesDialogProps) {
  const [comment, setComment] = useState('');
  const commentMissing = environment.isProtected && comment.trim() === '';
  return (
    <Dialog
      open={open}
      onClose={pending ? undefined : onClose}
      maxWidth="md"
      fullWidth
      aria-labelledby="review-changes-title"
    >
      <form
        onSubmit={(event) => {
          event.preventDefault();
          if (!commentMissing) {
            onSave(comment.trim());
          }
        }}
      >
        <DialogTitle id="review-changes-title">Review changes to {environment.name}</DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            <DialogContentText>
              Lines marked + are added and lines marked - are removed when you save.
            </DialogContentText>
            {error && <Alert severity="error">{error}</Alert>}
            <JsonDiff before={saved} after={draft} />
            <TextField
              label={environment.isProtected ? 'Comment (required)' : 'Comment (optional)'}
              value={comment}
              onChange={(event) => setComment(event.target.value)}
              multiline
              minRows={2}
              required={environment.isProtected}
              helperText={
                environment.isProtected
                  ? `${environment.name} is protected, so changes need a comment. The audit log records it.`
                  : 'The audit log records it with this change.'
              }
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose} disabled={pending}>
            Keep editing
          </Button>
          <Button type="submit" variant="contained" disabled={commentMissing} loading={pending}>
            Save changes
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
