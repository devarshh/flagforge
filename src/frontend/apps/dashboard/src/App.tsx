import { QueryClientProvider } from '@tanstack/react-query';
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider';
import { AdapterDayjs } from '@mui/x-date-pickers/AdapterDayjs';
import { createBrowserRouter, RouterProvider } from 'react-router';
import { createQueryClient } from './api/queryClient';
import { AuthProvider } from './auth/AuthProvider';
import { NotificationsProvider } from './components/NotificationsProvider';
import { routes } from './routes';
import { ColorModeProvider } from './theme/ColorModeProvider';

const queryClient = createQueryClient();
const router = createBrowserRouter(routes);

export function App() {
  return (
    <ColorModeProvider>
      <QueryClientProvider client={queryClient}>
        <LocalizationProvider dateAdapter={AdapterDayjs}>
          <NotificationsProvider>
            <AuthProvider>
              <RouterProvider router={router} />
            </AuthProvider>
          </NotificationsProvider>
        </LocalizationProvider>
      </QueryClientProvider>
    </ColorModeProvider>
  );
}
