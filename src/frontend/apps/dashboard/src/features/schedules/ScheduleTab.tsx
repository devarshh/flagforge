import {
  Alert,
  Box,
  Button,
  Skeleton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import { useMemo, useState } from 'react';
import { errorMessage } from '../../api/errors';
import type { Environment, Flag, ScheduledChange, Targeting } from '../../api/types';
import { useCurrentUser } from '../../auth/authContext';
import { ConfirmDialog } from '../../components/ConfirmDialog';
import { EmptyState } from '../../components/EmptyState';
import { ErrorState } from '../../components/ErrorState';
import { RelativeTime } from '../../components/RelativeTime';
import { Section } from '../../components/Section';
import { ToneChip } from '../../components/ToneChip';
import { formatDateTime } from '../../components/time';
import { useNotify } from '../../components/notify';
import { changeBlockedReason } from '../flags/permissions';
import { describeChange, statusLabels } from './describe';
import { ReleasePlanDialog } from './ReleasePlanDialog';
import { ScheduleChangeDialog } from './ScheduleChangeDialog';
import { useCancelSchedule, useSchedules } from './schedulesApi';

export interface ScheduleTabProps {
  projectKey: string;
  flag: Flag;
  environment: Environment;
  saved: Targeting;
}

/** "Step 2 of 4" for changes that belong to a release plan. */
function planSteps(changes: readonly ScheduledChange[]): Map<string, string> {
  const plans = new Map<string, ScheduledChange[]>();
  for (const change of changes) {
    if (change.releasePlanId) {
      plans.set(change.releasePlanId, [...(plans.get(change.releasePlanId) ?? []), change]);
    }
  }

  const labels = new Map<string, string>();
  for (const steps of plans.values()) {
    steps.forEach((change, index) =>
      labels.set(change.id, `Release plan, step ${index + 1} of ${steps.length}`),
    );
  }

  return labels;
}

export function ScheduleTab({ projectKey, flag, environment, saved }: ScheduleTabProps) {
  const user = useCurrentUser();
  const notify = useNotify();
  const schedules = useSchedules(projectKey, flag.key, environment.key);
  const cancel = useCancelSchedule(projectKey, flag.key, environment.key);
  const [dialog, setDialog] = useState<'change' | 'plan' | null>(null);
  const [cancelling, setCancelling] = useState<ScheduledChange | null>(null);
  const blockedReason = changeBlockedReason(user.role, environment, flag.isArchived);
  const stepLabels = useMemo(() => planSteps(schedules.data ?? []), [schedules.data]);

  const actions = blockedReason === null && (
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
      <Button variant="outlined" onClick={() => setDialog('plan')}>
        Create release plan
      </Button>
      <Button variant="contained" onClick={() => setDialog('change')}>
        Schedule a change
      </Button>
    </Stack>
  );

  return (
    <Section
      title="Scheduled changes"
      description="Changes apply automatically at the chosen time, within about 15 seconds."
      actions={actions}
    >
      <Stack spacing={2}>
        {blockedReason && <Alert severity="info">{blockedReason}</Alert>}
        {schedules.error && (
          <ErrorState error={schedules.error} onRetry={() => void schedules.refetch()} />
        )}
        {schedules.isPending && <Skeleton variant="rounded" height={120} />}
        {schedules.data?.length === 0 && (
          <EmptyState
            title="Nothing scheduled"
            description="Schedule a change to turn this flag on or off later, or create a release plan to roll it out gradually."
          />
        )}
        {schedules.data && schedules.data.length > 0 && (
          <TableContainer>
            <Table size="small" aria-label={`Scheduled changes in ${environment.name}`}>
              <TableHead>
                <TableRow>
                  <TableCell>When</TableCell>
                  <TableCell>Change</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell>Scheduled by</TableCell>
                  <TableCell>
                    <span className="visually-hidden">Actions</span>
                  </TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {schedules.data.map((change) => {
                  const status = statusLabels[change.status];
                  return (
                    <TableRow key={change.id}>
                      <TableCell sx={{ whiteSpace: 'nowrap' }}>
                        <Box>{formatDateTime(change.executeAt)}</Box>
                        <Typography variant="body2" color="text.secondary">
                          <RelativeTime value={change.executeAt} />
                        </Typography>
                      </TableCell>
                      <TableCell>
                        <Box>{describeChange(change, flag.variations)}</Box>
                        {stepLabels.has(change.id) && (
                          <Typography variant="body2" color="text.secondary">
                            {stepLabels.get(change.id)}
                          </Typography>
                        )}
                      </TableCell>
                      <TableCell>
                        <ToneChip label={status.label} tone={status.tone} />
                        {change.status === 'failed' && change.error && (
                          <Typography variant="body2" color="error" sx={{ mt: 0.5 }}>
                            {change.error}
                          </Typography>
                        )}
                      </TableCell>
                      <TableCell>{change.createdBy.displayName}</TableCell>
                      <TableCell align="right">
                        {change.status === 'pending' && blockedReason === null && (
                          <Button size="small" color="error" onClick={() => setCancelling(change)}>
                            Cancel
                          </Button>
                        )}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </Stack>
      <ScheduleChangeDialog
        open={dialog === 'change'}
        onClose={() => setDialog(null)}
        projectKey={projectKey}
        flagKey={flag.key}
        environment={environment}
        variations={flag.variations}
        fallthrough={saved.fallthrough}
      />
      <ReleasePlanDialog
        open={dialog === 'plan'}
        onClose={() => setDialog(null)}
        projectKey={projectKey}
        flagKey={flag.key}
        environment={environment}
        variations={flag.variations}
        fallthrough={saved.fallthrough}
      />
      <ConfirmDialog
        open={cancelling !== null}
        title="Cancel this scheduled change?"
        description={
          cancelling
            ? `${describeChange(cancelling, flag.variations)} on ${formatDateTime(cancelling.executeAt)} will not happen.`
            : ''
        }
        confirmLabel="Cancel change"
        cancelLabel="Keep it"
        destructive
        pending={cancel.isPending}
        onConfirm={() =>
          cancelling &&
          cancel.mutate(cancelling.id, {
            onSuccess: () => {
              setCancelling(null);
              notify('Scheduled change cancelled');
            },
            onError: (error) => notify(errorMessage(error), 'error'),
          })
        }
        onClose={() => setCancelling(null)}
      />
    </Section>
  );
}
