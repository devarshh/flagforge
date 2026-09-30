import {
  Button,
  Card,
  CardActionArea,
  CardContent,
  Grid,
  Skeleton,
  Stack,
  Typography,
} from '@mui/material';
import AddRounded from '@mui/icons-material/AddRounded';
import { useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { hasRole, useCurrentUser } from '../../auth/authContext';
import { EmptyState } from '../../components/EmptyState';
import { EnvironmentChip } from '../../components/EnvironmentChip';
import { ErrorState } from '../../components/ErrorState';
import { PageHeader } from '../../components/PageHeader';
import { CreateProjectDialog } from './CreateProjectDialog';
import { useProjects } from './projectsApi';

export function ProjectsPage() {
  const user = useCurrentUser();
  const { data: projects, isPending, error, refetch } = useProjects();
  const [creating, setCreating] = useState(false);
  const canCreate = hasRole(user, 'admin');
  const createButton = canCreate && (
    <Button variant="contained" startIcon={<AddRounded />} onClick={() => setCreating(true)}>
      Create project
    </Button>
  );

  return (
    <>
      <PageHeader
        title="Projects"
        subtitle="Each project has its own flags, environments, and SDK keys."
        actions={createButton}
      />
      {error && <ErrorState error={error} onRetry={() => void refetch()} />}
      {isPending && (
        <Grid container spacing={2}>
          {[0, 1, 2].map((index) => (
            <Grid key={index} size={{ xs: 12, sm: 6, lg: 4 }}>
              <Skeleton variant="rounded" height={132} />
            </Grid>
          ))}
        </Grid>
      )}
      {projects?.length === 0 && (
        <EmptyState
          title="No projects yet"
          description={
            canCreate
              ? 'Create a project to start managing flags for an app or service.'
              : 'Ask an admin to create a project.'
          }
          action={createButton}
        />
      )}
      <Grid container spacing={2}>
        {projects?.map((project) => (
          <Grid key={project.key} size={{ xs: 12, sm: 6, lg: 4 }}>
            <Card sx={{ height: '100%' }}>
              <CardActionArea
                component={RouterLink}
                to={`/projects/${project.key}/flags`}
                sx={{ height: '100%' }}
              >
                <CardContent>
                  <Typography variant="h4" component="h2">
                    {project.name}
                  </Typography>
                  <Typography className="mono" variant="body2" color="text.secondary">
                    {project.key}
                  </Typography>
                  {project.description && (
                    <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
                      {project.description}
                    </Typography>
                  )}
                  <Stack direction="row" sx={{ mt: 2, flexWrap: 'wrap', gap: 0.75 }}>
                    {project.environments.map((environment) => (
                      <EnvironmentChip
                        key={environment.key}
                        name={environment.name}
                        color={environment.color}
                        isProtected={environment.isProtected}
                      />
                    ))}
                  </Stack>
                </CardContent>
              </CardActionArea>
            </Card>
          </Grid>
        ))}
      </Grid>
      <CreateProjectDialog open={creating} onClose={() => setCreating(false)} />
    </>
  );
}
