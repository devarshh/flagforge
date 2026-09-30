import { zodResolver } from '@hookform/resolvers/zod';
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
  TextField,
  Typography,
} from '@mui/material';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { ApiError, errorMessage } from '../../api/errors';
import type { Environment } from '../../api/types';
import { CopyButton } from '../../components/CopyButton';
import { useNotify } from '../../components/notify';
import { useCreateSdkKey } from './sdkKeysApi';

const schema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'Name the key after the app that uses it.')
    .max(100, 'Names can be at most 100 characters.'),
});

type SdkKeyForm = z.infer<typeof schema>;

export interface CreateSdkKeyDialogProps {
  projectKey: string;
  environment: Environment;
  open: boolean;
  onClose: () => void;
}

export function CreateSdkKeyDialog(props: CreateSdkKeyDialogProps) {
  // Remounting on open forgets the previous plaintext key.
  return props.open ? <CreateSdkKeyDialogContent {...props} /> : null;
}

/** Creates a key, then shows its plaintext once: FlagForge stores only a hash. */
function CreateSdkKeyDialogContent({
  projectKey,
  environment,
  open,
  onClose,
}: CreateSdkKeyDialogProps) {
  const notify = useNotify();
  const create = useCreateSdkKey(projectKey, environment.key);
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<SdkKeyForm>({ resolver: zodResolver(schema), defaultValues: { name: '' } });

  const onSubmit = handleSubmit(({ name }) =>
    create.mutate(name.trim(), {
      onSuccess: () => notify('SDK key created'),
      onError: (error) => {
        if (error instanceof ApiError && error.errors.name?.[0]) {
          setError('name', { message: error.errors.name[0] });
        }
      },
    }),
  );

  const created = create.data;
  return (
    <Dialog
      open={open}
      onClose={create.isPending ? undefined : onClose}
      maxWidth="sm"
      fullWidth
      aria-labelledby="create-sdk-key-title"
    >
      {created ? (
        <>
          <DialogTitle id="create-sdk-key-title">Copy your SDK key</DialogTitle>
          <DialogContent>
            <Stack spacing={2}>
              <Alert severity="warning">
                Copy this key now. FlagForge stores only a hash of it, so it cannot show it again.
              </Alert>
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
                  aria-label="SDK key"
                >
                  {created.plaintextKey}
                </Typography>
                <CopyButton value={created.plaintextKey} label="Copy SDK key" />
              </Box>
              <DialogContentText>
                SDK keys identify the {environment.name} environment to your apps. They are not
                secrets: browser apps include them in their code. Revoke a key to stop it working
                within seconds.
              </DialogContentText>
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button variant="contained" onClick={onClose}>
              Done
            </Button>
          </DialogActions>
        </>
      ) : (
        <form noValidate onSubmit={(event) => void onSubmit(event)}>
          <DialogTitle id="create-sdk-key-title">
            Create an SDK key for {environment.name}
          </DialogTitle>
          <DialogContent>
            <Stack spacing={2} sx={{ pt: 1 }}>
              {create.isError &&
                !(create.error instanceof ApiError && create.error.errors.name) && (
                  <Alert severity="error">{errorMessage(create.error)}</Alert>
                )}
              <TextField
                label="Name"
                autoFocus
                placeholder="Web storefront"
                {...register('name')}
                error={Boolean(errors.name)}
                helperText={errors.name?.message ?? 'Name the key after the app that uses it.'}
              />
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={onClose} disabled={create.isPending}>
              Cancel
            </Button>
            <Button type="submit" variant="contained" loading={create.isPending}>
              Create key
            </Button>
          </DialogActions>
        </form>
      )}
    </Dialog>
  );
}
