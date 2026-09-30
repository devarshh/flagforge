import { Alert, Snackbar } from '@mui/material';
import { useCallback, useState, type ReactNode } from 'react';
import { NotifyContext, type NotifySeverity } from './notify';

interface Notification {
  id: number;
  message: string;
  severity: NotifySeverity;
}

export function NotificationsProvider({ children }: { children?: ReactNode }) {
  const [current, setCurrent] = useState<Notification | null>(null);
  const notify = useCallback((message: string, severity: NotifySeverity = 'success') => {
    setCurrent((previous) => ({ id: (previous?.id ?? 0) + 1, message, severity }));
  }, []);

  return (
    <NotifyContext.Provider value={notify}>
      {children}
      <Snackbar
        key={current?.id}
        open={current !== null}
        autoHideDuration={current?.severity === 'error' ? 8000 : 4000}
        onClose={(_, reason) => {
          if (reason !== 'clickaway') {
            setCurrent(null);
          }
        }}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
      >
        <Alert
          severity={current?.severity ?? 'success'}
          variant="filled"
          onClose={() => setCurrent(null)}
        >
          {current?.message}
        </Alert>
      </Snackbar>
    </NotifyContext.Provider>
  );
}
