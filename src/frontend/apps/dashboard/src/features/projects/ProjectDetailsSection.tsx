import { zodResolver } from '@hookform/resolvers/zod';
import { Alert, Box, Button, Stack, TextField } from '@mui/material';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { ApiError, errorMessage } from '../../api/errors';
import type { Project } from '../../api/types';
import { Section } from '../../components/Section';
import { useNotify } from '../../components/notify';
import { useUpdateProject } from './projectsApi';

const schema = z.object({
  name: z.string().trim().min(1, 'Enter a name.').max(100, 'Names can be at most 100 characters.'),
  description: z.string().max(1000, 'Descriptions can be at most 1000 characters.'),
});

type DetailsForm = z.infer<typeof schema>;

export function ProjectDetailsSection({
  project,
  canEdit,
}: {
  project: Project;
  canEdit: boolean;
}) {
  const notify = useNotify();
  const update = useUpdateProject(project.key);
  const defaults: DetailsForm = { name: project.name, description: project.description ?? '' };
  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isDirty },
  } = useForm<DetailsForm>({
    resolver: zodResolver(schema),
    values: defaults,
    resetOptions: { keepDirtyValues: true },
  });

  const onSubmit = handleSubmit((form) =>
    update.mutate(
      { name: form.name.trim(), description: form.description.trim() },
      {
        onSuccess: (saved) => {
          reset({ name: saved.name, description: saved.description ?? '' });
          notify('Changes saved');
        },
        onError: (error) => {
          if (error instanceof ApiError) {
            for (const [path, messages] of Object.entries(error.errors)) {
              if ((path === 'name' || path === 'description') && messages[0]) {
                setError(path, { message: messages[0] });
              }
            }
          }
        },
      },
    ),
  );

  return (
    <Section title="Details">
      <Stack
        component="form"
        noValidate
        spacing={2.5}
        onSubmit={(event) => void onSubmit(event)}
        sx={{ maxWidth: 640 }}
      >
        {update.isError &&
          !(update.error instanceof ApiError && Object.keys(update.error.errors).length > 0) && (
            <Alert severity="error">{errorMessage(update.error)}</Alert>
          )}
        <TextField
          label="Name"
          disabled={!canEdit}
          {...register('name')}
          error={Boolean(errors.name)}
          helperText={errors.name?.message}
        />
        <TextField
          label="Key"
          value={project.key}
          disabled
          helperText="Keys cannot change."
          slotProps={{ htmlInput: { className: 'mono' } }}
        />
        <TextField
          label="Description"
          multiline
          minRows={2}
          disabled={!canEdit}
          {...register('description')}
          error={Boolean(errors.description)}
          helperText={errors.description?.message}
        />
        {canEdit && (
          <Box sx={{ display: 'flex', gap: 1 }}>
            <Button
              type="submit"
              variant="contained"
              disabled={!isDirty}
              loading={update.isPending}
            >
              Save changes
            </Button>
            <Button disabled={!isDirty || update.isPending} onClick={() => reset(defaults)}>
              Discard
            </Button>
          </Box>
        )}
      </Stack>
    </Section>
  );
}
