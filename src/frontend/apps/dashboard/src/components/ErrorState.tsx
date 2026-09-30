import { Alert, Button } from '@mui/material';
import { errorMessage } from '../api/errors';

export interface ErrorStateProps {
  error: unknown;
  onRetry?: () => void;
}

export function ErrorState({ error, onRetry }: ErrorStateProps) {
  return (
    <Alert
      severity="error"
      action={
        onRetry && (
          <Button color="inherit" size="small" onClick={onRetry}>
            Try again
          </Button>
        )
      }
    >
      {errorMessage(error)}
    </Alert>
  );
}
