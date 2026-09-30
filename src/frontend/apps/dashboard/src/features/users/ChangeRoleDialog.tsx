import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  Radio,
  RadioGroup,
  Stack,
  Typography,
} from '@mui/material';
import { useState } from 'react';
import { errorMessage } from '../../api/errors';
import type { Role, User } from '../../api/types';
import { useNotify } from '../../components/notify';
import { roleLabel, roleOptions } from './roles';
import { useUpdateUser } from './usersApi';

export function ChangeRoleDialog({ user, onClose }: { user: User | null; onClose: () => void }) {
  return user ? <ChangeRoleDialogContent user={user} onClose={onClose} /> : null;
}

function ChangeRoleDialogContent({ user, onClose }: { user: User; onClose: () => void }) {
  const notify = useNotify();
  const update = useUpdateUser();
  const [role, setRole] = useState<Role>(user.role);
  return (
    <Dialog
      open
      onClose={update.isPending ? undefined : onClose}
      maxWidth="sm"
      fullWidth
      aria-labelledby="change-role-title"
    >
      <DialogTitle id="change-role-title">Change role for {user.displayName}</DialogTitle>
      <DialogContent>
        <Stack spacing={2}>
          {update.isError && <Alert severity="error">{errorMessage(update.error)}</Alert>}
          <RadioGroup
            value={role}
            onChange={(event) => setRole(event.target.value as Role)}
            aria-label="Role"
          >
            {roleOptions.map((option) => (
              <FormControlLabel
                key={option.role}
                value={option.role}
                control={<Radio />}
                sx={{ alignItems: 'flex-start', mb: 1 }}
                label={
                  <span>
                    {option.label}
                    <Typography
                      component="span"
                      variant="body2"
                      color="text.secondary"
                      sx={{ display: 'block' }}
                    >
                      {option.description}
                    </Typography>
                  </span>
                }
              />
            ))}
          </RadioGroup>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={update.isPending}>
          Cancel
        </Button>
        <Button
          variant="contained"
          disabled={role === user.role}
          loading={update.isPending}
          onClick={() =>
            update.mutate(
              { userId: user.id, changes: { role } },
              {
                onSuccess: () => {
                  notify(
                    `${user.displayName} is now ${roleLabel(role) === 'Admin' ? 'an' : 'a'} ${roleLabel(role)}`,
                  );
                  onClose();
                },
              },
            )
          }
        >
          Change role
        </Button>
      </DialogActions>
    </Dialog>
  );
}
