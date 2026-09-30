import { zodResolver } from '@hookform/resolvers/zod';
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  Stack,
  TextField,
} from '@mui/material';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { ApiError, errorMessage } from '../../api/errors';
import { useNotify } from '../../components/notify';
import { roleOptions } from './roles';
import { useCreateUser, type CreatedUser } from './usersApi';

const schema = z.object({
  email: z
    .string()
    .trim()
    .min(1, 'Enter an email address.')
    .max(256, 'Email addresses can be at most 256 characters.')
    .email('Enter a valid email address, such as sam@example.com.'),
  displayName: z
    .string()
    .trim()
    .min(1, 'Enter a display name.')
    .max(100, 'Display names can be at most 100 characters.'),
  role: z.enum(['viewer', 'editor', 'admin']),
});

type CreateUserForm = z.infer<typeof schema>;

export interface CreateUserDialogProps {
  open: boolean;
  onClose: () => void;
  onCreated: (created: CreatedUser) => void;
}

export function CreateUserDialog(props: CreateUserDialogProps) {
  return props.open ? <CreateUserDialogContent {...props} /> : null;
}

function CreateUserDialogContent({ open, onClose, onCreated }: CreateUserDialogProps) {
  const notify = useNotify();
  const create = useCreateUser();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<CreateUserForm>({
    resolver: zodResolver(schema),
    defaultValues: { email: '', displayName: '', role: 'editor' },
  });

  const onSubmit = handleSubmit((form) =>
    create.mutate(
      { email: form.email.trim(), displayName: form.displayName.trim(), role: form.role },
      {
        onSuccess: (created) => {
          notify('User created');
          onCreated(created);
        },
        onError: (error) => {
          if (!(error instanceof ApiError)) {
            return;
          }

          if (error.status === 409) {
            setError('email', { message: error.detail ?? error.title });
          }

          for (const [path, messages] of Object.entries(error.errors)) {
            if ((path === 'email' || path === 'displayName') && messages[0]) {
              setError(path, { message: messages[0] });
            }
          }
        },
      },
    ),
  );

  const fieldErrorShown =
    create.error instanceof ApiError &&
    (create.error.status === 409 || Object.keys(create.error.errors).length > 0);
  return (
    <Dialog
      open={open}
      onClose={create.isPending ? undefined : onClose}
      maxWidth="sm"
      fullWidth
      aria-labelledby="create-user-title"
    >
      <form noValidate onSubmit={(event) => void onSubmit(event)}>
        <DialogTitle id="create-user-title">Add user</DialogTitle>
        <DialogContent>
          <Stack spacing={2.5} sx={{ pt: 1 }}>
            {create.isError && !fieldErrorShown && (
              <Alert severity="error">{errorMessage(create.error)}</Alert>
            )}
            <TextField
              label="Email"
              type="email"
              autoFocus
              {...register('email')}
              error={Boolean(errors.email)}
              helperText={errors.email?.message}
            />
            <TextField
              label="Display name"
              {...register('displayName')}
              error={Boolean(errors.displayName)}
              helperText={errors.displayName?.message}
            />
            <TextField
              select
              label="Role"
              defaultValue="editor"
              {...register('role')}
              helperText="You can change the role later."
            >
              {roleOptions.map((option) => (
                <MenuItem key={option.role} value={option.role}>
                  {option.label}
                </MenuItem>
              ))}
            </TextField>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose} disabled={create.isPending}>
            Cancel
          </Button>
          <Button type="submit" variant="contained" loading={create.isPending}>
            Add user
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
