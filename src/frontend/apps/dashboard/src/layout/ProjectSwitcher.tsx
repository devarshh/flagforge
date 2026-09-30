import { Button, ListItemText, Menu, MenuItem } from '@mui/material';
import UnfoldMoreRounded from '@mui/icons-material/UnfoldMoreRounded';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { useProjects } from '../features/projects/projectsApi';

/** Switches the current project; the choice lives in the URL, so it is shareable and survives reloads. */
export function ProjectSwitcher({ projectKey }: { projectKey: string | undefined }) {
  const navigate = useNavigate();
  const { data: projects } = useProjects();
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const current = projects?.find((project) => project.key === projectKey);

  return (
    <>
      <Button
        color="inherit"
        endIcon={<UnfoldMoreRounded />}
        onClick={(event) => setAnchor(event.currentTarget)}
        aria-haspopup="menu"
        aria-label={current ? `Project: ${current.name}. Switch project` : 'Choose a project'}
        sx={{ maxWidth: 240, justifyContent: 'space-between' }}
      >
        <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
          {current?.name ?? 'Choose a project'}
        </span>
      </Button>
      <Menu anchorEl={anchor} open={anchor !== null} onClose={() => setAnchor(null)}>
        {(projects ?? []).map((project) => (
          <MenuItem
            key={project.key}
            selected={project.key === projectKey}
            onClick={() => {
              setAnchor(null);
              void navigate(`/projects/${project.key}/flags`);
            }}
          >
            <ListItemText
              primary={project.name}
              secondary={project.key}
              slotProps={{ secondary: { className: 'mono' } }}
            />
          </MenuItem>
        ))}
        <MenuItem
          onClick={() => {
            setAnchor(null);
            void navigate('/projects');
          }}
        >
          All projects
        </MenuItem>
      </Menu>
    </>
  );
}
