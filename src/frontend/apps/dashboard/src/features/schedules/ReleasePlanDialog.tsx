import {
  Alert,
  Autocomplete,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  IconButton,
  InputAdornment,
  MenuItem,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import AddRounded from '@mui/icons-material/AddRounded';
import DeleteOutlineRounded from '@mui/icons-material/DeleteOutlineRounded';
import { DateTimePicker } from '@mui/x-date-pickers/DateTimePicker';
import dayjs from 'dayjs';
import { useState } from 'react';
import { ApiError, errorMessage } from '../../api/errors';
import type { Environment, Serve, Variation, WeightedVariation } from '../../api/types';
import { formatDateTime, formatRelative } from '../../components/time';
import { withMono } from '../../components/mono';
import { useNotify } from '../../components/notify';
import { variationColor } from '../../theme/tokens';
import { attributeSuggestions } from '../targeting/operators';
import { percentToWeight, totalWeight, weightToPercent } from '../targeting/rollout';
import { isValidAttribute } from '../targeting/validation';
import { VariationLabel } from '../targeting/VariationLabel';
import { defaultSteps, maxSteps, validateSteps, type Step } from './releasePlan';
import { useCreateReleasePlan } from './schedulesApi';

export interface ReleasePlanDialogProps {
  open: boolean;
  onClose: () => void;
  projectKey: string;
  flagKey: string;
  environment: Environment;
  variations: readonly Variation[];
  fallthrough: Serve;
}

export function ReleasePlanDialog(props: ReleasePlanDialogProps) {
  // Remounting on open starts every plan from the default steps.
  return props.open ? <ReleasePlanDialogContent {...props} /> : null;
}

function ReleasePlanDialogContent({
  open,
  onClose,
  projectKey,
  flagKey,
  environment,
  variations,
  fallthrough,
}: ReleasePlanDialogProps) {
  const notify = useNotify();
  const create = useCreateReleasePlan(projectKey, flagKey, environment.key);
  const current = fallthrough.variationId ?? null;
  const [rolloutId, setRolloutId] = useState(
    () => variations.find((variation) => variation.id !== current)?.id ?? variations[0]?.id ?? '',
  );
  const [restId, setRestId] = useState(() =>
    current && current !== rolloutId
      ? current
      : (variations.find((variation) => variation.id !== rolloutId)?.id ?? ''),
  );
  const [steps, setSteps] = useState<Step[]>(defaultSteps);
  const [bucketBy, setBucketBy] = useState('key');
  const [submitted, setSubmitted] = useState(false);
  const now = dayjs();

  const clientErrors = validateSteps(steps, now);
  const sameVariation = rolloutId === restId;
  const bucketError = isValidAttribute(bucketBy.trim() || 'key')
    ? null
    : 'Bucket by a valid attribute name, such as key.';
  const serverErrors = create.error instanceof ApiError ? create.error.errors : {};
  const stepError = (index: number, field: 'executeAt' | 'percent') =>
    (submitted ? clientErrors[`steps[${index}].${field}`] : undefined) ??
    (field === 'executeAt'
      ? serverErrors[`steps[${index}].executeAt`]?.[0]
      : serverErrors[`steps[${index}].weights`]?.[0]);
  const rolloutIndex = variations.findIndex((variation) => variation.id === rolloutId);
  const restIndex = variations.findIndex((variation) => variation.id === restId);

  const weightsFor = (percent: string): WeightedVariation[] => {
    const weight = percentToWeight(percent) ?? 0;
    return [
      { variationId: rolloutId, weight },
      { variationId: restId, weight: totalWeight - weight },
    ].filter((item) => item.weight > 0);
  };

  const submit = () => {
    setSubmitted(true);
    if (Object.keys(clientErrors).length > 0 || sameVariation || bucketError) {
      return;
    }

    create.mutate(
      {
        steps: steps.map((step) => ({
          executeAt: step.executeAt!.toISOString(),
          weights: weightsFor(step.percent),
        })),
        bucketBy: bucketBy.trim() || 'key',
      },
      {
        onSuccess: () => {
          notify('Release plan created');
          onClose();
        },
      },
    );
  };

  const updateStep = (uid: number, changes: Partial<Step>) =>
    setSteps((items) => items.map((item) => (item.uid === uid ? { ...item, ...changes } : item)));
  const rolloutName = variations[rolloutIndex]?.name ?? 'the new variation';

  return (
    <Dialog
      open={open}
      onClose={create.isPending ? undefined : onClose}
      maxWidth="md"
      fullWidth
      aria-labelledby="release-plan-title"
    >
      <form
        noValidate
        onSubmit={(event) => {
          event.preventDefault();
          submit();
        }}
      >
        <DialogTitle id="release-plan-title">
          Create a release plan for {environment.name}
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2.5}>
            <DialogContentText>
              Each step changes the default rule to a percentage rollout at the chosen time.
              Contexts keep their bucket between steps, so people who already have the new variation
              keep it.
            </DialogContentText>
            {create.isError && Object.keys(serverErrors).length === 0 && (
              <Alert severity="error">{errorMessage(create.error)}</Alert>
            )}
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
              <TextField
                select
                fullWidth
                label="Roll out"
                value={rolloutId}
                onChange={(event) => setRolloutId(event.target.value)}
              >
                {variations.map((variation, index) => (
                  <MenuItem key={variation.id} value={variation.id}>
                    <VariationLabel variation={variation} index={index} />
                  </MenuItem>
                ))}
              </TextField>
              <TextField
                select
                fullWidth
                label="Everyone else gets"
                value={restId}
                onChange={(event) => setRestId(event.target.value)}
                error={submitted && sameVariation}
                helperText={
                  submitted && sameVariation
                    ? 'Choose a different variation from the one you roll out.'
                    : undefined
                }
              >
                {variations.map((variation, index) => (
                  <MenuItem key={variation.id} value={variation.id}>
                    <VariationLabel variation={variation} index={index} />
                  </MenuItem>
                ))}
              </TextField>
            </Stack>
            <Stack spacing={1.5}>
              <Typography variant="subtitle2" component="h3">
                Steps
              </Typography>
              {steps.map((step, index) => (
                <Stack
                  key={step.uid}
                  direction={{ xs: 'column', sm: 'row' }}
                  spacing={1.5}
                  sx={{ alignItems: { sm: 'flex-start' } }}
                  role="group"
                  aria-label={`Step ${index + 1}`}
                >
                  <DateTimePicker
                    label={`Step ${index + 1} time`}
                    value={step.executeAt}
                    onChange={(value) => updateStep(step.uid, { executeAt: value })}
                    disablePast
                    slotProps={{
                      textField: {
                        error: Boolean(stepError(index, 'executeAt')),
                        helperText: stepError(index, 'executeAt'),
                        sx: { flex: 1 },
                      },
                    }}
                  />
                  <TextField
                    label={`${rolloutName} share`}
                    value={step.percent}
                    onChange={(event) => updateStep(step.uid, { percent: event.target.value })}
                    error={Boolean(stepError(index, 'percent'))}
                    helperText={stepError(index, 'percent')}
                    sx={{ width: { sm: 180 } }}
                    slotProps={{
                      input: { endAdornment: <InputAdornment position="end">%</InputAdornment> },
                      htmlInput: { inputMode: 'decimal' },
                    }}
                  />
                  <Tooltip
                    title={
                      steps.length === 1
                        ? 'A plan needs at least one step'
                        : `Remove step ${index + 1}`
                    }
                  >
                    <span>
                      <IconButton
                        aria-label={`Remove step ${index + 1}`}
                        disabled={steps.length === 1}
                        onClick={() =>
                          setSteps((items) => items.filter((item) => item.uid !== step.uid))
                        }
                        sx={{ mt: 0.5 }}
                      >
                        <DeleteOutlineRounded />
                      </IconButton>
                    </span>
                  </Tooltip>
                </Stack>
              ))}
              <Box>
                <Button
                  startIcon={<AddRounded />}
                  disabled={steps.length >= maxSteps}
                  onClick={() =>
                    setSteps((items) => {
                      const last = items[items.length - 1];
                      const base = last?.executeAt?.isValid()
                        ? last.executeAt
                        : dayjs().startOf('minute');
                      return [
                        ...items,
                        {
                          uid: Math.max(0, ...items.map((item) => item.uid)) + 1,
                          executeAt: base.add(1, 'day'),
                          percent: '100',
                        },
                      ];
                    })
                  }
                >
                  Add step
                </Button>
              </Box>
            </Stack>
            <Autocomplete
              freeSolo
              disableClearable
              options={attributeSuggestions}
              value={bucketBy}
              inputValue={bucketBy}
              onInputChange={(_, value) => setBucketBy(value)}
              onChange={(_, value) => setBucketBy(value)}
              sx={{ maxWidth: 280 }}
              renderInput={(params) => (
                <TextField
                  {...params}
                  label="Bucket by"
                  error={submitted && Boolean(bucketError)}
                  helperText={
                    (submitted && bucketError) ||
                    'Contexts with the same value get the same variation.'
                  }
                  slotProps={{
                    ...params.slotProps,
                    htmlInput: {
                      ...params.slotProps.htmlInput,
                      className: withMono(params.slotProps.htmlInput.className),
                    },
                  }}
                />
              )}
            />
            <Box component="section" aria-labelledby="timeline-title">
              <Typography id="timeline-title" variant="subtitle2" component="h3" sx={{ mb: 1 }}>
                Timeline
              </Typography>
              <Box component="ol" sx={{ listStyle: 'none', p: 0, m: 0 }}>
                {steps.map((step, index) => {
                  const weight = percentToWeight(step.percent);
                  const valid = step.executeAt?.isValid() && weight !== null;
                  return (
                    <Box
                      component="li"
                      key={step.uid}
                      sx={{
                        display: 'grid',
                        gridTemplateColumns: '16px 1fr',
                        columnGap: 1.5,
                        pb: index < steps.length - 1 ? 2 : 0,
                      }}
                    >
                      <Box
                        sx={{ position: 'relative', display: 'flex', justifyContent: 'center' }}
                        aria-hidden
                      >
                        <Box
                          sx={{
                            width: 10,
                            height: 10,
                            mt: 0.75,
                            borderRadius: '50%',
                            bgcolor: 'primary.main',
                            zIndex: 1,
                          }}
                        />
                        {index < steps.length - 1 && (
                          <Box
                            sx={{
                              position: 'absolute',
                              top: 16,
                              bottom: -16,
                              width: 2,
                              bgcolor: 'divider',
                            }}
                          />
                        )}
                      </Box>
                      <Box sx={{ minWidth: 0 }}>
                        <Typography variant="body2">
                          {step.executeAt?.isValid()
                            ? `${formatDateTime(step.executeAt.toDate())} (${formatRelative(step.executeAt.toDate(), now.valueOf())})`
                            : 'Choose a time'}
                        </Typography>
                        {valid && (
                          <>
                            <Box
                              sx={{
                                display: 'flex',
                                height: 8,
                                borderRadius: 1,
                                overflow: 'hidden',
                                bgcolor: 'action.hover',
                                my: 0.75,
                                maxWidth: 420,
                              }}
                            >
                              <Box
                                sx={{
                                  width: `${weight / 1000}%`,
                                  bgcolor: variationColor(Math.max(rolloutIndex, 0)),
                                }}
                              />
                              <Box
                                sx={{
                                  width: `${(totalWeight - weight) / 1000}%`,
                                  bgcolor: variationColor(Math.max(restIndex, 0)),
                                }}
                              />
                            </Box>
                            <Typography variant="body2" color="text.secondary">
                              {weightToPercent(weight)}% get {rolloutName}
                            </Typography>
                          </>
                        )}
                      </Box>
                    </Box>
                  );
                })}
              </Box>
            </Box>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose} disabled={create.isPending}>
            Cancel
          </Button>
          <Button type="submit" variant="contained" loading={create.isPending}>
            Create release plan
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
