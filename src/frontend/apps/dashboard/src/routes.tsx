import { Navigate, type RouteObject } from 'react-router';
import { ChangePasswordPage } from './auth/ChangePasswordPage';
import { LoginPage } from './auth/LoginPage';
import { RequireAuth } from './auth/RequireAuth';
import { ProjectsPage } from './features/projects/ProjectsPage';
import { AppShell } from './layout/AppShell';
import { NotFoundPage, PageSkeleton, RouteError } from './layout/RouteError';

/** Heavier pages load on demand so the first screen stays small. */
export const routes: RouteObject[] = [
  { path: '/login', element: <LoginPage /> },
  {
    path: '/',
    element: (
      <RequireAuth>
        <AppShell />
      </RequireAuth>
    ),
    hydrateFallbackElement: <PageSkeleton />,
    errorElement: <RouteError />,
    children: [
      {
        // A pathless route whose error boundary renders inside the shell.
        errorElement: <RouteError />,
        children: [
          { index: true, element: <Navigate to="/projects" replace /> },
          { path: 'projects', element: <ProjectsPage /> },
          { path: 'projects/:projectKey', element: <Navigate to="flags" replace /> },
          {
            path: 'projects/:projectKey/flags',
            lazy: () =>
              import('./features/flags/FlagListPage').then((module) => ({
                Component: module.FlagListPage,
              })),
          },
          {
            path: 'projects/:projectKey/flags/:flagKey',
            lazy: () =>
              import('./features/flags/FlagDetailPage').then((module) => ({
                Component: module.FlagDetailPage,
              })),
          },
          {
            path: 'projects/:projectKey/stale',
            lazy: () =>
              import('./features/stale/StaleFlagsPage').then((module) => ({
                Component: module.StaleFlagsPage,
              })),
          },
          {
            path: 'projects/:projectKey/settings',
            lazy: () =>
              import('./features/projects/ProjectSettingsPage').then((module) => ({
                Component: module.ProjectSettingsPage,
              })),
          },
          {
            path: 'audit',
            lazy: () =>
              import('./features/audit/AuditPage').then((module) => ({
                Component: module.AuditPage,
              })),
          },
          {
            path: 'admin/users',
            lazy: () =>
              import('./features/users/UsersPage').then((module) => ({
                Component: module.UsersPage,
              })),
          },
          { path: 'account/password', element: <ChangePasswordPage /> },
          { path: '*', element: <NotFoundPage /> },
        ],
      },
    ],
  },
];
