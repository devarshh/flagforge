import { zodResolver } from '@hookform/resolvers/zod';
import { Alert, Box, Button, Paper, Stack, TextField, Typography } from '@mui/material';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Navigate, useLocation, useNavigate } from 'react-router';
import { z } from 'zod';
import { errorMessage } from '../api/errors';
import { useAuth } from './authContext';

const schema = z.object({
  email: z.string().trim().min(1, 'Enter your email address.'),
  password: z.string().min(1, 'Enter your password.'),
});

type LoginForm = z.infer<typeof schema>;

export function LoginPage() {
  const { state, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [error, setError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginForm>({
    resolver: zodResolver(schema),
    defaultValues: { email: '', password: '' },
  });
  const from = (location.state as { from?: string } | null)?.from ?? '/projects';

  if (state.status === 'signedIn') {
    return <Navigate to={state.user.mustChangePassword ? '/account/password' : from} replace />;
  }

  const onSubmit = handleSubmit(async ({ email, password }) => {
    setError(null);
    try {
      const user = await login(email, password);
      await navigate(user.mustChangePassword ? '/account/password' : from, { replace: true });
    } catch (caught) {
      setError(errorMessage(caught));
    }
  });

  return (
    <Box
      component="main"
      sx={{
        minHeight: '100vh',
        display: 'grid',
        placeItems: 'center',
        px: 2,
        bgcolor: 'background.default',
      }}
    >
      <Paper
        variant="outlined"
        sx={{ p: { xs: 3, sm: 4 }, width: '100%', maxWidth: 400, borderRadius: 2 }}
      >
        <Stack component="form" noValidate spacing={2.5} onSubmit={(event) => void onSubmit(event)}>
          <Box>
            <Typography variant="h2" component="h1">
              Sign in to FlagForge
            </Typography>
            <Typography color="text.secondary" sx={{ mt: 0.5 }}>
              Change what your apps do without deploying.
            </Typography>
          </Box>
          {error && <Alert severity="error">{error}</Alert>}
          <TextField
            label="Email"
            type="email"
            autoComplete="username"
            autoFocus
            {...register('email')}
            error={Boolean(errors.email)}
            helperText={errors.email?.message}
          />
          <TextField
            label="Password"
            type="password"
            autoComplete="current-password"
            {...register('password')}
            error={Boolean(errors.password)}
            helperText={errors.password?.message}
          />
          <Button type="submit" variant="contained" size="large" loading={isSubmitting}>
            Sign in
          </Button>
        </Stack>
      </Paper>
    </Box>
  );
}
