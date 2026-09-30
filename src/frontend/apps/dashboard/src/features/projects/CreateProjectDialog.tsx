import { zodResolver } from '@hookform/resolvers/zod';
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  TextField,
} from '@mui/material';
import { useForm, useWatch } from 'react-hook-form';
import { useNavigate } from 'react-router';
import { z } from 'zod';
import { ApiError, errorMessage } from '../../api/errors';
import { useNotify } from '../../components/notify';
import { keyPattern, keyRequirement, suggestKey } from './keys';
import { useCreateProject } from './projectsApi';

const schema = z.object({
  name: z.string().trim().min(1, 'Enter a name.').max(100, 'Names can be at most 100 characters.'),
  key: z.string().regex(keyPattern, keyRequirement),
  description: z.string().max(1000, 'Descriptions can be at most 1000 characters.'),
});

type ProjectForm = z.infer<typeof schema>;

export function CreateProjectDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const createProject = useCreateProject();
  const navigate = useNavigate();
  const notify = useNotify();
  const {
    control,
    register,
    handleSubmit,
    setValue,
    setError,
    getFieldState,
    reset,
    formState: { errors },
  } = useForm<ProjectForm>({
    resolver: zodResolver(schema),
    defaultValues: { name: '', key: '', description: '' },
  });
  const keyValue = useWatch({ control, name: 'key' });

  const close = () => {
    reset();
    createProject.reset();
    onClose();
  };

  const onSubmit = handleSubmit((form) =>
    createProject.mutate(
      { key: form.key, name: form.name, description: form.description || undefined },
      {
        onSuccess: (project) => {
          notify('Project created');
          close();
          void navigate(`/projects/${project.key}/flags`);
        },
        onError: (error) => {
          if (error instanceof ApiError && error.errors.key) {
            setError('key', { message: error.errors.key[0] });
          }
        },
      },
    ),
  );

  const nameField = register('name');
  return (
    <Dialog
      open={open}
      onClose={close}
      maxWidth="sm"
      fullWidth
      aria-labelledby="create-project-title"
    >
      <form noValidate onSubmit={(event) => void onSubmit(event)}>
        <DialogTitle id="create-project-title">Create project</DialogTitle>
        <DialogContent>
          <Stack spacing={2.5} sx={{ pt: 1 }}>
            {createProject.isError &&
              !(createProject.error instanceof ApiError && createProject.error.errors.key) && (
                <Alert severity="error">{errorMessage(createProject.error)}</Alert>
              )}
            <TextField
              label="Name"
              autoFocus
              {...nameField}
              onChange={(event) => {
                void nameField.onChange(event);
                if (!getFieldState('key').isDirty) {
                  setValue('key', suggestKey(event.target.value));
                }
              }}
              error={Boolean(errors.name)}
              helperText={errors.name?.message}
            />
            <TextField
              label="Key"
              {...register('key')}
              value={keyValue}
              error={Boolean(errors.key)}
              helperText={
                errors.key?.message ?? 'Used in URLs and the audit log. It cannot change later.'
              }
              slotProps={{ htmlInput: { className: 'mono', spellCheck: false } }}
            />
            <TextField
              label="Description"
              multiline
              minRows={2}
              {...register('description')}
              error={Boolean(errors.description)}
              helperText={errors.description?.message}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={close}>Cancel</Button>
          <Button type="submit" variant="contained" loading={createProject.isPending}>
            Create project
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
