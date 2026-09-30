import {
  Box,
  Button,
  Link,
  Paper,
  Skeleton,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import { useState } from 'react';
import { Link as RouterLink, useParams } from 'react-router';
import { errorMessage } from '../../api/errors';
import type { StaleFlag } from '../../api/types';
import { useCurrentUser } from '../../auth/authContext';
import { ConfirmDialog } from '../../components/ConfirmDialog';
import { EmptyState } from '../../components/EmptyState';
import { ErrorState } from '../../components/ErrorState';
import { PageHeader } from '../../components/PageHeader';
import { RelativeTime } from '../../components/RelativeTime';
import { ToneChip } from '../../components/ToneChip';
import { useNotify } from '../../components/notify';
import { useSetArchived } from '../flags/flagsApi';
import { canEditFlags } from '../flags/permissions';
import { useStaleFlags } from './staleApi';

function reasonText(flag: StaleFlag): string {
  return flag.reason === 'noRecentEvaluations'
    ? 'Not evaluated in the last 30 days'
    : 'Served a single variation everywhere for 14 days';
}

export function StaleFlagsPage() {
  const { projectKey = '' } = useParams();
  const user = useCurrentUser();
  const notify = useNotify();
  const stale = useStaleFlags(projectKey);
  const setArchived = useSetArchived(projectKey);
  const [archiving, setArchiving] = useState<StaleFlag | null>(null);
  const canArchive = canEditFlags(user.role);

  return (
    <>
      <PageHeader
        title="Stale flags"
        subtitle="Flags older than 30 days that nothing evaluates any more, or that serve one variation everywhere. Remove them from your code, then archive them."
      />
      {stale.error && <ErrorState error={stale.error} onRetry={() => void stale.refetch()} />}
      {stale.isPending && <Skeleton variant="rounded" height={200} />}
      {stale.data?.length === 0 && (
        <EmptyState
          title="No stale flags"
          description="Nothing to clean up. Flags appear here when they are not evaluated for 30 days, or when every environment serves a single variation."
        />
      )}
      {stale.data && stale.data.length > 0 && (
        <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: 2 }}>
          <Table aria-label="Stale flags">
            <TableHead>
              <TableRow>
                <TableCell>Flag</TableCell>
                <TableCell>Reason</TableCell>
                <TableCell>Last evaluated</TableCell>
                <TableCell>
                  <span className="visually-hidden">Actions</span>
                </TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {stale.data.map((flag) => (
                <TableRow key={flag.flagKey}>
                  <TableCell>
                    <Link
                      component={RouterLink}
                      to={`/projects/${projectKey}/flags/${flag.flagKey}`}
                      underline="hover"
                      sx={{ fontWeight: 500 }}
                    >
                      {flag.name}
                    </Link>
                    <Typography className="mono" variant="body2" color="text.secondary">
                      {flag.flagKey}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <ToneChip
                      label={
                        flag.reason === 'noRecentEvaluations'
                          ? 'No recent evaluations'
                          : 'Fully rolled out'
                      }
                      tone="warning"
                    />
                    <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                      {reasonText(flag)}
                      {flag.servedVariationId && (
                        <>
                          {': '}
                          <Box component="span" className="mono">
                            {flag.servedVariationId}
                          </Box>
                        </>
                      )}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <RelativeTime value={flag.lastEvaluatedAt} resolution="hour" />
                  </TableCell>
                  <TableCell align="right">
                    {canArchive && (
                      <Button size="small" variant="outlined" onClick={() => setArchiving(flag)}>
                        Archive
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
      <ConfirmDialog
        open={archiving !== null}
        title={`Archive ${archiving?.name ?? 'flag'}?`}
        description="SDKs stop receiving this flag and fall back to their default values. You can restore it later."
        confirmLabel="Archive flag"
        destructive
        pending={setArchived.isPending}
        onConfirm={() =>
          archiving &&
          setArchived.mutate(
            { flagKey: archiving.flagKey, archive: true },
            {
              onSuccess: () => {
                setArchiving(null);
                notify('Flag archived');
              },
              onError: (error) => notify(errorMessage(error), 'error'),
            },
          )
        }
        onClose={() => setArchiving(null)}
      />
    </>
  );
}
