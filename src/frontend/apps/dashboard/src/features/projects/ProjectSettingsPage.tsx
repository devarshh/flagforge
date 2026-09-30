import { Alert, Skeleton, Stack } from '@mui/material';
import { useMemo } from 'react';
import { useParams } from 'react-router';
import { hasRole, useCurrentUser } from '../../auth/authContext';
import { ErrorState } from '../../components/ErrorState';
import { PageHeader } from '../../components/PageHeader';
import { Section } from '../../components/Section';
import { EnvironmentsSection } from '../environments/EnvironmentsSection';
import { sortEnvironments } from '../environments/order';
import { SdkKeysSection } from '../sdk-keys/SdkKeysSection';
import { DeleteProjectSection } from './DeleteProjectSection';
import { ProjectDetailsSection } from './ProjectDetailsSection';
import { useProject } from './projectsApi';

export function ProjectSettingsPage() {
  const { projectKey = '' } = useParams();
  const user = useCurrentUser();
  const project = useProject(projectKey);
  const isAdmin = hasRole(user, 'admin');
  const environments = useMemo(
    () => sortEnvironments(project.data?.environments ?? []),
    [project.data],
  );

  return (
    <>
      <PageHeader title="Project settings" subtitle={project.data?.name} />
      {project.error && <ErrorState error={project.error} onRetry={() => void project.refetch()} />}
      {project.isPending && <Skeleton variant="rounded" height={320} />}
      {project.data && (
        <Stack spacing={2.5}>
          {!isAdmin && <Alert severity="info">Only admins can change project settings.</Alert>}
          <ProjectDetailsSection project={project.data} canEdit={isAdmin} />
          <EnvironmentsSection
            projectKey={projectKey}
            environments={environments}
            canEdit={isAdmin}
          />
          {isAdmin ? (
            <SdkKeysSection projectKey={projectKey} environments={environments} />
          ) : (
            <Section
              title="SDK keys"
              description="Only admins can see and create SDK keys. Ask an admin for a key for your app."
            />
          )}
          {isAdmin && <DeleteProjectSection project={project.data} />}
        </Stack>
      )}
    </>
  );
}
