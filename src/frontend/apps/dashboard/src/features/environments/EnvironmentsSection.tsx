import {
  Button,
  IconButton,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
} from '@mui/material';
import AddRounded from '@mui/icons-material/AddRounded';
import ArrowDownwardRounded from '@mui/icons-material/ArrowDownwardRounded';
import ArrowUpwardRounded from '@mui/icons-material/ArrowUpwardRounded';
import DeleteOutlineRounded from '@mui/icons-material/DeleteOutlineRounded';
import EditOutlined from '@mui/icons-material/EditOutlined';
import { useState, type ReactNode } from 'react';
import { errorMessage } from '../../api/errors';
import type { Environment } from '../../api/types';
import { ConfirmDialog } from '../../components/ConfirmDialog';
import { EnvironmentChip } from '../../components/EnvironmentChip';
import { Section } from '../../components/Section';
import { TypeToConfirmDialog } from '../../components/TypeToConfirmDialog';
import { useNotify } from '../../components/notify';
import { EnvironmentDialog } from './EnvironmentDialog';
import {
  useDeleteEnvironment,
  useReorderEnvironments,
  useUpdateEnvironment,
} from './environmentsApi';

export interface EnvironmentsSectionProps {
  projectKey: string;
  /** In display order. */
  environments: readonly Environment[];
  canEdit: boolean;
}

export function EnvironmentsSection({
  projectKey,
  environments,
  canEdit,
}: EnvironmentsSectionProps) {
  const notify = useNotify();
  const update = useUpdateEnvironment(projectKey);
  const reorder = useReorderEnvironments(projectKey);
  const remove = useDeleteEnvironment(projectKey);
  const [editing, setEditing] = useState<Environment | null>(null);
  const [adding, setAdding] = useState(false);
  const [unprotecting, setUnprotecting] = useState<Environment | null>(null);
  const [deleting, setDeleting] = useState<Environment | null>(null);

  const setProtected = (environment: Environment, isProtected: boolean) =>
    update.mutate(
      { environmentKey: environment.key, changes: { isProtected } },
      {
        onSuccess: () => {
          setUnprotecting(null);
          notify(
            isProtected
              ? `${environment.name} is now protected`
              : `${environment.name} is no longer protected`,
          );
        },
        onError: (error) => notify(errorMessage(error), 'error'),
      },
    );

  const move = (index: number, offset: -1 | 1) => {
    const ordered = [...environments];
    const [moved] = ordered.splice(index, 1);
    if (!moved) {
      return;
    }

    ordered.splice(index + offset, 0, moved);
    reorder.mutate(ordered, { onError: (error) => notify(errorMessage(error), 'error') });
  };

  const action = (label: string, icon: ReactNode, onClick: () => void, enabled = true) => (
    <Tooltip title={label}>
      <span>
        <IconButton
          aria-label={label}
          size="small"
          disabled={!enabled || reorder.isPending}
          onClick={onClick}
        >
          {icon}
        </IconButton>
      </span>
    </Tooltip>
  );

  return (
    <Section
      title="Environments"
      description="Each environment has its own targeting and SDK keys. Protected environments need an admin and a comment for every change."
      actions={
        canEdit && (
          <Button variant="outlined" startIcon={<AddRounded />} onClick={() => setAdding(true)}>
            Add environment
          </Button>
        )
      }
    >
      <TableContainer>
        <Table size="small" aria-label="Environments">
          <TableHead>
            <TableRow>
              <TableCell>Environment</TableCell>
              <TableCell>Key</TableCell>
              <TableCell>Protected</TableCell>
              {canEdit && (
                <TableCell>
                  <span className="visually-hidden">Actions</span>
                </TableCell>
              )}
            </TableRow>
          </TableHead>
          <TableBody>
            {environments.map((environment, index) => (
              <TableRow key={environment.key}>
                <TableCell>
                  <EnvironmentChip
                    name={environment.name}
                    color={environment.color}
                    isProtected={environment.isProtected}
                  />
                </TableCell>
                <TableCell>
                  <Typography className="mono" variant="body2">
                    {environment.key}
                  </Typography>
                </TableCell>
                <TableCell>
                  {canEdit ? (
                    <Switch
                      checked={environment.isProtected}
                      disabled={update.isPending}
                      slotProps={{ input: { 'aria-label': `${environment.name} is protected` } }}
                      onChange={(event) =>
                        event.target.checked
                          ? setProtected(environment, true)
                          : setUnprotecting(environment)
                      }
                    />
                  ) : environment.isProtected ? (
                    'Yes'
                  ) : (
                    'No'
                  )}
                </TableCell>
                {canEdit && (
                  <TableCell align="right" sx={{ whiteSpace: 'nowrap' }}>
                    {action(
                      `Move ${environment.name} up`,
                      <ArrowUpwardRounded fontSize="small" />,
                      () => move(index, -1),
                      index > 0,
                    )}
                    {action(
                      `Move ${environment.name} down`,
                      <ArrowDownwardRounded fontSize="small" />,
                      () => move(index, 1),
                      index < environments.length - 1,
                    )}
                    {action(`Edit ${environment.name}`, <EditOutlined fontSize="small" />, () =>
                      setEditing(environment),
                    )}
                    {action(
                      `Delete ${environment.name}`,
                      <DeleteOutlineRounded fontSize="small" />,
                      () => setDeleting(environment),
                      environments.length > 1,
                    )}
                  </TableCell>
                )}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      <EnvironmentDialog
        projectKey={projectKey}
        environment={editing}
        open={adding || editing !== null}
        onClose={() => {
          setAdding(false);
          setEditing(null);
        }}
      />
      <ConfirmDialog
        open={unprotecting !== null}
        title={`Remove protection from ${unprotecting?.name ?? 'this environment'}?`}
        description="Editors will be able to change flags here, and changes will no longer need a comment."
        confirmLabel="Remove protection"
        destructive
        pending={update.isPending}
        onConfirm={() => unprotecting && setProtected(unprotecting, false)}
        onClose={() => setUnprotecting(null)}
      />
      <TypeToConfirmDialog
        open={deleting !== null}
        title={`Delete ${deleting?.name ?? 'environment'}?`}
        description="This deletes its targeting for every flag, its scheduled changes, usage history, and SDK keys. Apps using its SDK keys stop receiving flags."
        confirmText={deleting?.key ?? ''}
        confirmLabel="Delete environment"
        destructive
        pending={remove.isPending}
        error={remove.isError ? errorMessage(remove.error) : null}
        onConfirm={() =>
          deleting &&
          remove.mutate(deleting.key, {
            onSuccess: () => {
              notify('Environment deleted');
              setDeleting(null);
            },
          })
        }
        onClose={() => {
          remove.reset();
          setDeleting(null);
        }}
      />
    </Section>
  );
}
