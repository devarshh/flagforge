import { zodResolver } from '@hookform/resolvers/zod';
import {
  Alert,
  Box,
  Button,
  FormControlLabel,
  Stack,
  Switch,
  TextField,
  Typography,
} from '@mui/material';
import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useNavigate } from 'react-router';
import { z } from 'zod';
import { ApiError, errorMessage } from '../../api/errors';
import type { Flag } from '../../api/types';
import { hasRole, useCurrentUser } from '../../auth/authContext';
import { ChipInput } from '../../components/ChipInput';
import { Section } from '../../components/Section';
import { TypeToConfirmDialog } from '../../components/TypeToConfirmDialog';
import { useNotify } from '../../components/notify';
import { ArchiveFlagButton } from './ArchiveFlagButton';
import { useDeleteFlag, useUpdateFlag } from './flagsApi';
import { canEditFlags } from './permissions';
import { tagsSchema } from './tags';

const schema = z.object({
  name: z.string().trim().min(1, 'Enter a name.').max(100, 'Names can be at most 100 characters.'),
  description: z.string().max(1000, 'Descriptions can be at most 1000 characters.'),
  tags: tagsSchema,
  isPermanent: z.boolean(),
});

type SettingsForm = z.infer<typeof schema>;

export function FlagSettingsTab({ projectKey, flag }: { projectKey: string; flag: Flag }) {
  const user = useCurrentUser();
  const notify = useNotify();
  const navigate = useNavigate();
  const update = useUpdateFlag(projectKey, flag.key);
  const deleteFlag = useDeleteFlag(projectKey, flag.key);
  const [deleting, setDeleting] = useState(false);
  const canEdit = canEditFlags(user.role);
  const defaults: SettingsForm = {
    name: flag.name,
    description: flag.description ?? '',
    tags: flag.tags,
    isPermanent: flag.isPermanent,
  };
  const {
    register,
    control,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isDirty },
  } = useForm<SettingsForm>({
    resolver: zodResolver(schema),
    values: defaults,
    resetOptions: { keepDirtyValues: true },
  });

  const onSubmit = handleSubmit((form) =>
    update.mutate(
      {
        name: form.name.trim(),
        description: form.description.trim(),
        tags: form.tags,
        isPermanent: form.isPermanent,
      },
      {
        onSuccess: (saved) => {
          reset({
            name: saved.name,
            description: saved.description ?? '',
            tags: saved.tags,
            isPermanent: saved.isPermanent,
          });
          notify('Changes saved');
        },
        onError: (error) => {
          if (error instanceof ApiError) {
            for (const [path, messages] of Object.entries(error.errors)) {
              const message = messages[0];
              if (!message) continue;
              if (path === 'name' || path === 'description') setError(path, { message });
              else if (path.startsWith('tags')) setError('tags', { message });
            }
          }
        },
      },
    ),
  );

  return (
    <Stack spacing={2.5}>
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
            label="Description"
            multiline
            minRows={2}
            disabled={!canEdit}
            {...register('description')}
            error={Boolean(errors.description)}
            helperText={errors.description?.message}
          />
          <Controller
            control={control}
            name="tags"
            render={({ field }) => (
              <ChipInput
                label="Tags"
                value={field.value}
                onChange={field.onChange}
                disabled={!canEdit}
                error={Boolean(errors.tags)}
                helperText={errors.tags?.message ?? 'Up to 10 tags.'}
              />
            )}
          />
          <Controller
            control={control}
            name="isPermanent"
            render={({ field }) => (
              <FormControlLabel
                control={
                  <Switch
                    checked={field.value}
                    disabled={!canEdit}
                    onChange={(event) => field.onChange(event.target.checked)}
                  />
                }
                label="Permanent flag (never reported as stale)"
              />
            )}
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
      {canEdit && (
        <Section
          title={flag.isArchived ? 'Restore' : 'Archive'}
          description={
            flag.isArchived
              ? 'Restoring makes the flag available to SDKs again, with the targeting it had when it was archived.'
              : 'Archived flags are no longer served: SDKs fall back to their default values. Pending scheduled changes are cancelled.'
          }
        >
          <ArchiveFlagButton projectKey={projectKey} flag={flag} />
        </Section>
      )}
      {hasRole(user, 'admin') && (
        <Section
          title="Delete flag"
          description="Deleting removes the flag and its targeting in every environment. The audit log keeps its history."
        >
          {!flag.isArchived && (
            <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
              Archive the flag first. Only archived flags can be deleted.
            </Typography>
          )}
          <Button
            variant="outlined"
            color="error"
            disabled={!flag.isArchived}
            onClick={() => setDeleting(true)}
          >
            Delete flag
          </Button>
          <TypeToConfirmDialog
            open={deleting}
            title={`Delete ${flag.name}?`}
            description="This cannot be undone. Code that still reads this flag gets its default value."
            confirmText={flag.key}
            confirmLabel="Delete flag"
            destructive
            pending={deleteFlag.isPending}
            error={deleteFlag.isError ? errorMessage(deleteFlag.error) : null}
            onConfirm={() =>
              deleteFlag.mutate(undefined, {
                onSuccess: () => {
                  notify('Flag deleted');
                  void navigate(`/projects/${projectKey}/flags`, { replace: true });
                },
              })
            }
            onClose={() => setDeleting(false)}
          />
        </Section>
      )}
    </Stack>
  );
}
