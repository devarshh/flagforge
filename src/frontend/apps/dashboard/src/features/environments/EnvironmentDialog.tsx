import { zodResolver } from '@hookform/resolvers/zod';
import {
  Alert,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  FormHelperText,
  Stack,
  Switch,
  TextField,
  Typography,
} from '@mui/material';
import CheckRounded from '@mui/icons-material/CheckRounded';
import { Controller, useForm, useWatch } from 'react-hook-form';
import { z } from 'zod';
import { ApiError, errorMessage } from '../../api/errors';
import type { Environment } from '../../api/types';
import { useNotify } from '../../components/notify';
import { environmentPalette } from '../../theme/tokens';
import { keyPattern, keyRequirement, suggestKey } from '../projects/keys';
import { useCreateEnvironment, useUpdateEnvironment } from './environmentsApi';

const schema = z.object({
  name: z.string().trim().min(1, 'Enter a name.').max(100, 'Names can be at most 100 characters.'),
  key: z.string().regex(keyPattern, keyRequirement),
  color: z.string().regex(/^#[0-9A-Fa-f]{6}$/, 'Choose a color.'),
  isProtected: z.boolean(),
});

type EnvironmentForm = z.infer<typeof schema>;

export interface EnvironmentDialogProps {
  projectKey: string;
  /** The environment to edit, or null to create one. */
  environment: Environment | null;
  open: boolean;
  onClose: () => void;
}

export function EnvironmentDialog(props: EnvironmentDialogProps) {
  // Remounting on open loads the environment being edited (or blank values).
  return props.open ? <EnvironmentDialogContent {...props} /> : null;
}

function EnvironmentDialogContent({
  projectKey,
  environment,
  open,
  onClose,
}: EnvironmentDialogProps) {
  const notify = useNotify();
  const create = useCreateEnvironment(projectKey);
  const update = useUpdateEnvironment(projectKey);
  const mutation = environment ? update : create;
  const {
    register,
    control,
    handleSubmit,
    setValue,
    setError,
    getFieldState,
    formState: { errors },
  } = useForm<EnvironmentForm>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: environment?.name ?? '',
      key: environment?.key ?? '',
      color: environment?.color ?? environmentPalette[0],
      isProtected: environment?.isProtected ?? false,
    },
  });
  const keyValue = useWatch({ control, name: 'key' });

  const onError = (error: Error) => {
    if (error instanceof ApiError) {
      for (const [path, messages] of Object.entries(error.errors)) {
        if ((path === 'name' || path === 'key' || path === 'color') && messages[0]) {
          setError(path, { message: messages[0] });
        }
      }

      if (error.status === 409 && !environment) {
        setError('key', { message: error.detail ?? error.title });
      }
    }
  };

  const onSubmit = handleSubmit((form) => {
    if (environment) {
      update.mutate(
        {
          environmentKey: environment.key,
          changes: { name: form.name.trim(), color: form.color, isProtected: form.isProtected },
        },
        {
          onSuccess: () => {
            notify('Environment saved');
            onClose();
          },
          onError,
        },
      );
    } else {
      create.mutate(
        { key: form.key, name: form.name.trim(), color: form.color, isProtected: form.isProtected },
        {
          onSuccess: () => {
            notify('Environment created');
            onClose();
          },
          onError,
        },
      );
    }
  });

  const nameField = register('name');
  const fieldErrorShown =
    mutation.error instanceof ApiError &&
    (Object.keys(mutation.error.errors).length > 0 || mutation.error.status === 409);
  return (
    <Dialog
      open={open}
      onClose={mutation.isPending ? undefined : onClose}
      maxWidth="sm"
      fullWidth
      aria-labelledby="environment-dialog-title"
    >
      <form noValidate onSubmit={(event) => void onSubmit(event)}>
        <DialogTitle id="environment-dialog-title">
          {environment ? `Edit ${environment.name}` : 'Add environment'}
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2.5} sx={{ pt: 1 }}>
            {mutation.isError && !fieldErrorShown && (
              <Alert severity="error">{errorMessage(mutation.error)}</Alert>
            )}
            <TextField
              label="Name"
              autoFocus
              {...nameField}
              onChange={(event) => {
                void nameField.onChange(event);
                if (!environment && !getFieldState('key').isDirty) {
                  setValue('key', suggestKey(event.target.value));
                }
              }}
              error={Boolean(errors.name)}
              helperText={errors.name?.message}
            />
            <TextField
              label="Key"
              disabled={environment !== null}
              {...register('key')}
              value={keyValue}
              error={Boolean(errors.key)}
              helperText={
                errors.key?.message ??
                (environment
                  ? 'Keys cannot change.'
                  : 'Used in URLs and SDK key names. It cannot change later.')
              }
              slotProps={{ htmlInput: { className: 'mono', spellCheck: false } }}
            />
            <Controller
              control={control}
              name="color"
              render={({ field }) => (
                <Box component="fieldset" sx={{ border: 0, p: 0, m: 0 }}>
                  <Typography
                    component="legend"
                    variant="body2"
                    color="text.secondary"
                    sx={{ mb: 1 }}
                  >
                    Color
                  </Typography>
                  <Stack
                    direction="row"
                    sx={{ gap: 1, flexWrap: 'wrap' }}
                    role="radiogroup"
                    aria-label="Color"
                  >
                    {environmentPalette.map((color) => {
                      const selected = field.value.toLowerCase() === color.toLowerCase();
                      return (
                        <Box
                          key={color}
                          component="button"
                          type="button"
                          role="radio"
                          aria-checked={selected}
                          aria-label={color}
                          onClick={() => field.onChange(color)}
                          sx={{
                            width: 32,
                            height: 32,
                            borderRadius: '50%',
                            border: 2,
                            borderColor: selected ? 'text.primary' : 'transparent',
                            bgcolor: color,
                            cursor: 'pointer',
                            display: 'grid',
                            placeItems: 'center',
                            color: '#FFFFFF',
                            p: 0,
                          }}
                        >
                          {selected && <CheckRounded fontSize="small" />}
                        </Box>
                      );
                    })}
                  </Stack>
                  {errors.color && <FormHelperText error>{errors.color.message}</FormHelperText>}
                </Box>
              )}
            />
            <Controller
              control={control}
              name="isProtected"
              render={({ field }) => (
                <FormControlLabel
                  control={
                    <Switch
                      checked={field.value}
                      onChange={(event) => field.onChange(event.target.checked)}
                    />
                  }
                  label={
                    <span>
                      Protected
                      <Typography
                        component="span"
                        variant="body2"
                        color="text.secondary"
                        sx={{ display: 'block' }}
                      >
                        Only admins can change flags here, and every change needs a comment.
                      </Typography>
                    </span>
                  }
                />
              )}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose} disabled={mutation.isPending}>
            Cancel
          </Button>
          <Button type="submit" variant="contained" loading={mutation.isPending}>
            {environment ? 'Save changes' : 'Add environment'}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
