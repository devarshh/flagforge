import { zodResolver } from '@hookform/resolvers/zod';
import { Alert, Button, Paper, Stack, TextField } from '@mui/material';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useNavigate } from 'react-router';
import { z } from 'zod';
import { apiClient } from '../api/client';
import { ApiError, errorMessage } from '../api/errors';
import type { User } from '../api/types';
import { PageHeader } from '../components/PageHeader';
import { useNotify } from '../components/notify';
import { useAuth, useCurrentUser } from './authContext';

const schema = z
  .object({
    currentPassword: z.string().min(1, 'Enter your current password.'),
    newPassword: z
      .string()
      .min(10, 'Use at least 10 characters.')
      .max(256, 'Use at most 256 characters.'),
    confirmPassword: z.string(),
  })
  .refine((form) => form.newPassword === form.confirmPassword, {
    path: ['confirmPassword'],
    message: 'The passwords do not match.',
  })
  .refine((form) => form.newPassword !== form.currentPassword, {
    path: ['newPassword'],
    message: 'Choose a password that is different from your current one.',
  });

type ChangePasswordForm = z.infer<typeof schema>;

export function ChangePasswordPage() {
  const user = useCurrentUser();
  const { setUser } = useAuth();
  const navigate = useNavigate();
  const notify = useNotify();
  const [error, setError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    setError: setFieldError,
    formState: { errors, isSubmitting },
  } = useForm<ChangePasswordForm>({
    resolver: zodResolver(schema),
    defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' },
  });

  const onSubmit = handleSubmit(async ({ currentPassword, newPassword }) => {
    setError(null);
    try {
      const updated = await apiClient.post<User>('/api/v1/auth/change-password', {
        currentPassword,
        newPassword,
      });
      setUser(updated);
      notify('Password changed');
      await navigate('/projects', { replace: true });
    } catch (caught) {
      if (caught instanceof ApiError && caught.errors.currentPassword) {
        setFieldError('currentPassword', { message: caught.errors.currentPassword[0] });
      } else {
        setError(errorMessage(caught));
      }
    }
  });

  return (
    <>
      <PageHeader
        title="Change password"
        subtitle={
          user.mustChangePassword
            ? 'You signed in with a temporary password. Choose your own to continue.'
            : 'Changing your password signs you out everywhere else.'
        }
      />
      <Paper variant="outlined" sx={{ p: 3, maxWidth: 480, borderRadius: 2 }}>
        <Stack component="form" noValidate spacing={2.5} onSubmit={(event) => void onSubmit(event)}>
          {error && <Alert severity="error">{error}</Alert>}
          <TextField
            label="Current password"
            type="password"
            autoComplete="current-password"
            {...register('currentPassword')}
            error={Boolean(errors.currentPassword)}
            helperText={errors.currentPassword?.message}
          />
          <TextField
            label="New password"
            type="password"
            autoComplete="new-password"
            {...register('newPassword')}
            error={Boolean(errors.newPassword)}
            helperText={errors.newPassword?.message ?? 'At least 10 characters.'}
          />
          <TextField
            label="Confirm new password"
            type="password"
            autoComplete="new-password"
            {...register('confirmPassword')}
            error={Boolean(errors.confirmPassword)}
            helperText={errors.confirmPassword?.message}
          />
          <Button
            type="submit"
            variant="contained"
            loading={isSubmitting}
            sx={{ alignSelf: 'flex-start' }}
          >
            Change password
          </Button>
        </Stack>
      </Paper>
    </>
  );
}
