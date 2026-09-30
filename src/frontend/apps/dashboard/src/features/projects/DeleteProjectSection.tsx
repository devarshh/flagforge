import { Button } from '@mui/material';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { errorMessage } from '../../api/errors';
import type { Project } from '../../api/types';
import { Section } from '../../components/Section';
import { TypeToConfirmDialog } from '../../components/TypeToConfirmDialog';
import { useNotify } from '../../components/notify';
import { useDeleteProject } from './projectsApi';

export function DeleteProjectSection({ project }: { project: Project }) {
  const notify = useNotify();
  const navigate = useNavigate();
  const remove = useDeleteProject(project.key);
  const [confirming, setConfirming] = useState(false);
  return (
    <Section
      title="Danger zone"
      description="Deleting a project removes its flags, environments, and SDK keys. Apps using its keys stop receiving flags. The audit log keeps its history."
    >
      <Button variant="outlined" color="error" onClick={() => setConfirming(true)}>
        Delete project
      </Button>
      <TypeToConfirmDialog
        open={confirming}
        title={`Delete ${project.name}?`}
        description="This cannot be undone."
        confirmText={project.key}
        confirmLabel="Delete project"
        destructive
        pending={remove.isPending}
        error={remove.isError ? errorMessage(remove.error) : null}
        onConfirm={() =>
          remove.mutate(undefined, {
            onSuccess: () => {
              notify('Project deleted');
              void navigate('/projects', { replace: true });
            },
          })
        }
        onClose={() => {
          remove.reset();
          setConfirming(false);
        }}
      />
    </Section>
  );
}
