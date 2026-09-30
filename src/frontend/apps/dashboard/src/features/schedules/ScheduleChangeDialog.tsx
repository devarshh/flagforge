import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { DateTimePicker } from '@mui/x-date-pickers/DateTimePicker';
import dayjs, { type Dayjs } from 'dayjs';
import { useState } from 'react';
import { ApiError, errorMessage } from '../../api/errors';
import type { Environment, ScheduledChangeAction, Serve, Variation } from '../../api/types';
import { useNotify } from '../../components/notify';
import { createServe, toServe, type DraftServe } from '../targeting/draft';
import { ServeEditor } from '../targeting/ServeEditor';
import { validateServeDraft } from '../targeting/validation';
import { useCreateSchedule } from './schedulesApi';

export interface ScheduleChangeDialogProps {
  open: boolean;
  onClose: () => void;
  projectKey: string;
  flagKey: string;
  environment: Environment;
  variations: readonly Variation[];
  /** The current default rule, the starting point for a default-rule change. */
  fallthrough: Serve;
}

const actionLabels: Record<ScheduledChangeAction, string> = {
  turnOn: 'Turn on',
  turnOff: 'Turn off',
  setFallthrough: 'Change the default rule',
};

export function ScheduleChangeDialog(props: ScheduleChangeDialogProps) {
  // Remounting on open starts each schedule from fresh defaults.
  return props.open ? <ScheduleChangeDialogContent {...props} /> : null;
}

function nextHour(): Dayjs {
  return dayjs().add(1, 'hour').startOf('hour');
}

function ScheduleChangeDialogContent({
  open,
  onClose,
  projectKey,
  flagKey,
  environment,
  variations,
  fallthrough,
}: ScheduleChangeDialogProps) {
  const notify = useNotify();
  const create = useCreateSchedule(projectKey, flagKey, environment.key);
  const [executeAt, setExecuteAt] = useState<Dayjs | null>(nextHour);
  const [action, setAction] = useState<ScheduledChangeAction>('turnOn');
  const [serve, setServe] = useState<DraftServe>(() => createServe(fallthrough, variations));
  const [submitted, setSubmitted] = useState(false);

  const serveErrors = action === 'setFallthrough' ? validateServeDraft(serve, 'payload') : {};
  const timeError =
    executeAt === null || !executeAt.isValid()
      ? 'Choose a date and time.'
      : executeAt.isBefore(dayjs())
        ? 'Choose a time in the future.'
        : null;
  const serverTimeError =
    create.error instanceof ApiError ? create.error.errors.executeAt?.[0] : undefined;

  const submit = () => {
    setSubmitted(true);
    if (timeError || Object.keys(serveErrors).length > 0 || executeAt === null) {
      return;
    }

    create.mutate(
      {
        executeAt: executeAt.toISOString(),
        action,
        payload: action === 'setFallthrough' ? toServe(serve) : undefined,
      },
      {
        onSuccess: () => {
          notify('Change scheduled');
          onClose();
        },
      },
    );
  };

  return (
    <Dialog
      open={open}
      onClose={create.isPending ? undefined : onClose}
      maxWidth="sm"
      fullWidth
      aria-labelledby="schedule-change-title"
    >
      <form
        noValidate
        onSubmit={(event) => {
          event.preventDefault();
          submit();
        }}
      >
        <DialogTitle id="schedule-change-title">
          Schedule a change in {environment.name}
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2.5} sx={{ pt: 1 }}>
            {create.isError && !serverTimeError && (
              <Alert severity="error">{errorMessage(create.error)}</Alert>
            )}
            <DateTimePicker
              label="When"
              value={executeAt}
              onChange={setExecuteAt}
              disablePast
              slotProps={{
                textField: {
                  error: Boolean((submitted && timeError) || serverTimeError),
                  helperText:
                    (submitted && timeError) || serverTimeError || 'Shown in your local time zone.',
                },
              }}
            />
            <TextField
              select
              label="Change"
              value={action}
              onChange={(event) => setAction(event.target.value as ScheduledChangeAction)}
            >
              {(Object.keys(actionLabels) as ScheduledChangeAction[]).map((option) => (
                <MenuItem key={option} value={option}>
                  {actionLabels[option]}
                </MenuItem>
              ))}
            </TextField>
            {action === 'setFallthrough' && (
              <Stack spacing={0.5}>
                <Typography variant="subtitle2" component="h3">
                  The default rule will serve
                </Typography>
                <ServeEditor
                  label="Scheduled default rule"
                  serve={serve}
                  onChange={setServe}
                  variations={variations}
                  errors={
                    submitted
                      ? serveErrors
                      : Object.fromEntries(
                          Object.entries(serveErrors).filter(([key]) => key.includes('.rollout')),
                        )
                  }
                  path="payload"
                />
              </Stack>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose} disabled={create.isPending}>
            Cancel
          </Button>
          <Button type="submit" variant="contained" loading={create.isPending}>
            Schedule change
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
