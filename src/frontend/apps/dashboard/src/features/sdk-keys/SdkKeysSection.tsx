import {
  Box,
  Button,
  MenuItem,
  Skeleton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import AddRounded from '@mui/icons-material/AddRounded';
import { useState } from 'react';
import { errorMessage } from '../../api/errors';
import type { Environment, SdkKey } from '../../api/types';
import { ConfirmDialog } from '../../components/ConfirmDialog';
import { EmptyState } from '../../components/EmptyState';
import { ErrorState } from '../../components/ErrorState';
import { RelativeTime } from '../../components/RelativeTime';
import { Section } from '../../components/Section';
import { ToneChip } from '../../components/ToneChip';
import { useNotify } from '../../components/notify';
import { CreateSdkKeyDialog } from './CreateSdkKeyDialog';
import { useRevokeSdkKey, useSdkKeys } from './sdkKeysApi';

export interface SdkKeysSectionProps {
  projectKey: string;
  environments: readonly Environment[];
}

/** SDK keys per environment: listed by prefix, created with a show-once dialog, revoked in seconds. */
export function SdkKeysSection({ projectKey, environments }: SdkKeysSectionProps) {
  const [selectedKey, setSelectedKey] = useState(environments[0]?.key ?? '');
  const environment = environments.find((item) => item.key === selectedKey) ?? environments[0];
  return (
    <Section
      title="SDK keys"
      description="Apps use an SDK key to read the flags of one environment. Keys are identifiers, not secrets."
    >
      {environment ? (
        <Stack spacing={2}>
          <TextField
            select
            label="Environment"
            value={environment.key}
            onChange={(event) => setSelectedKey(event.target.value)}
            sx={{ maxWidth: 280 }}
          >
            {environments.map((item) => (
              <MenuItem key={item.key} value={item.key}>
                {item.name}
              </MenuItem>
            ))}
          </TextField>
          <EnvironmentKeys
            key={environment.key}
            projectKey={projectKey}
            environment={environment}
          />
        </Stack>
      ) : (
        <Typography color="text.secondary">Add an environment to create SDK keys.</Typography>
      )}
    </Section>
  );
}

function EnvironmentKeys({
  projectKey,
  environment,
}: {
  projectKey: string;
  environment: Environment;
}) {
  const notify = useNotify();
  const keys = useSdkKeys(projectKey, environment.key);
  const revoke = useRevokeSdkKey(projectKey, environment.key);
  const [creating, setCreating] = useState(false);
  const [revoking, setRevoking] = useState<SdkKey | null>(null);
  const createButton = (
    <Button variant="outlined" startIcon={<AddRounded />} onClick={() => setCreating(true)}>
      Create SDK key
    </Button>
  );

  return (
    <>
      {keys.error && <ErrorState error={keys.error} onRetry={() => void keys.refetch()} />}
      {keys.isPending && <Skeleton variant="rounded" height={96} />}
      {keys.data?.length === 0 && (
        <EmptyState
          title={`No SDK keys for ${environment.name}`}
          description="Create a key so an app can read this environment's flags."
          action={createButton}
        />
      )}
      {keys.data && keys.data.length > 0 && (
        <Stack spacing={1.5}>
          <Box>{createButton}</Box>
          <TableContainer>
            <Table size="small" aria-label={`SDK keys for ${environment.name}`}>
              <TableHead>
                <TableRow>
                  <TableCell>Name</TableCell>
                  <TableCell>Key</TableCell>
                  <TableCell>Created</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell>
                    <span className="visually-hidden">Actions</span>
                  </TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {keys.data.map((key) => (
                  <TableRow key={key.id}>
                    <TableCell>{key.name}</TableCell>
                    <TableCell>
                      <Typography className="mono" variant="body2">
                        {key.keyPrefix}...
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <RelativeTime value={key.createdAt} />
                    </TableCell>
                    <TableCell>
                      {key.revokedAt ? (
                        <ToneChip label="Revoked" tone="neutral" />
                      ) : (
                        <ToneChip label="Active" tone="success" />
                      )}
                    </TableCell>
                    <TableCell align="right">
                      {!key.revokedAt && (
                        <Button size="small" color="error" onClick={() => setRevoking(key)}>
                          Revoke
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </Stack>
      )}
      <CreateSdkKeyDialog
        projectKey={projectKey}
        environment={environment}
        open={creating}
        onClose={() => setCreating(false)}
      />
      <ConfirmDialog
        open={revoking !== null}
        title={`Revoke ${revoking?.name ?? 'this key'}?`}
        description="Apps using this key stop receiving flags within seconds and fall back to their default values. This cannot be undone."
        confirmLabel="Revoke key"
        destructive
        pending={revoke.isPending}
        onConfirm={() =>
          revoking &&
          revoke.mutate(revoking.id, {
            onSuccess: () => {
              setRevoking(null);
              notify('SDK key revoked');
            },
            onError: (error) => notify(errorMessage(error), 'error'),
          })
        }
        onClose={() => setRevoking(null)}
      />
    </>
  );
}
