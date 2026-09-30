import {
  Alert,
  Box,
  Button,
  FormControlLabel,
  MenuItem,
  Paper,
  Stack,
  Switch,
  TextField,
  Typography,
} from '@mui/material';
import AddRounded from '@mui/icons-material/AddRounded';
import ScienceOutlined from '@mui/icons-material/ScienceOutlined';
import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useMemo, useReducer, useState } from 'react';
import { useBlocker } from 'react-router';
import { ApiError, errorMessage } from '../../api/errors';
import { queryKeys } from '../../api/queryKeys';
import type { Environment, EvaluationResult, Flag, Targeting } from '../../api/types';
import { useCurrentUser } from '../../auth/authContext';
import { ConfirmDialog } from '../../components/ConfirmDialog';
import { Section } from '../../components/Section';
import { useNotify } from '../../components/notify';
import { changeBlockedReason } from '../flags/permissions';
import { ConflictDialog } from './ConflictDialog';
import {
  createDraft,
  newRuleAction,
  sameConfig,
  targetingReducer,
  toConfig,
  type DraftAction,
  type TargetingDraft,
} from './draft';
import { IndividualTargeting } from './IndividualTargeting';
import { ReviewChangesDialog } from './ReviewChangesDialog';
import { RulePanel } from './RulePanel';
import { ServeEditor } from './ServeEditor';
import { useSaveTargeting } from './targetingApi';
import { TestPanel } from './TestPanel';
import {
  errorAt,
  fromServerErrors,
  liveErrors,
  validateDraft,
  type DraftErrors,
} from './validation';
import { VariationLabel } from './VariationLabel';

export interface TargetingTabProps {
  projectKey: string;
  flag: Flag;
  environment: Environment;
  saved: Targeting;
}

interface Baseline {
  /** The saved config the draft started from; a new object means the server has something newer. */
  source: Targeting;
  variationIds: string;
  draft: TargetingDraft;
}

const maxRules = 50;
const noErrors: DraftErrors = {};

function baselineOf(saved: Targeting, flag: Flag): Baseline {
  return {
    source: saved,
    variationIds: flag.variations.map((variation) => variation.id).join('|'),
    draft: createDraft(saved, flag.variations),
  };
}

/** Edits a draft of one environment's targeting; nothing changes on the server until the person reviews and saves. */
export function TargetingTab({ projectKey, flag, environment, saved }: TargetingTabProps) {
  const user = useCurrentUser();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const save = useSaveTargeting(projectKey, flag.key, environment.key);
  const [baseline, setBaseline] = useState(() => baselineOf(saved, flag));
  const [draft, dispatchDraft] = useReducer(targetingReducer, baseline.draft);
  const [serverErrors, setServerErrors] = useState<DraftErrors>(noErrors);
  const [showAllErrors, setShowAllErrors] = useState(false);
  const [reviewing, setReviewing] = useState(false);
  const [conflict, setConflict] = useState(false);
  const [testing, setTesting] = useState(false);
  const [match, setMatch] = useState<EvaluationResult | null>(null);

  const variations = flag.variations;
  const blockedReason = changeBlockedReason(user.role, environment, flag.isArchived);
  const disabled = blockedReason !== null;
  const savedConfig = useMemo(() => toConfig(baseline.draft), [baseline]);
  const draftConfig = useMemo(() => toConfig(draft), [draft]);
  const dirty = !sameConfig(savedConfig, draftConfig);
  const clientErrors = useMemo(() => validateDraft(draft), [draft]);
  const problemCount = Object.keys(clientErrors).length;
  const errors = useMemo(
    () => ({ ...(showAllErrors ? clientErrors : liveErrors(clientErrors)), ...serverErrors }),
    [showAllErrors, clientErrors, serverErrors],
  );

  // Take the newest saved config (a teammate's change, a toggle, or new variations) while there is nothing to lose.
  const variationIds = variations.map((variation) => variation.id).join('|');
  if (!dirty && (saved !== baseline.source || variationIds !== baseline.variationIds)) {
    const next = baselineOf(saved, flag);
    setBaseline(next);
    dispatchDraft({ type: 'reset', draft: next.draft });
  }

  const dispatch = useCallback((action: DraftAction) => {
    setServerErrors((current) => (current === noErrors ? current : noErrors));
    setMatch(null);
    dispatchDraft(action);
  }, []);

  const resetTo = (targeting: Targeting) => {
    const next = baselineOf(targeting, flag);
    setBaseline(next);
    dispatchDraft({ type: 'reset', draft: next.draft });
    setServerErrors(noErrors);
    setShowAllErrors(false);
  };

  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) =>
      dirty &&
      (currentLocation.pathname !== nextLocation.pathname ||
        currentLocation.search !== nextLocation.search),
  );

  useEffect(() => {
    if (!dirty) {
      return undefined;
    }

    const onBeforeUnload = (event: BeforeUnloadEvent) => event.preventDefault();
    window.addEventListener('beforeunload', onBeforeUnload);
    return () => window.removeEventListener('beforeunload', onBeforeUnload);
  }, [dirty]);

  const review = () => {
    if (problemCount > 0) {
      setShowAllErrors(true);
      return;
    }

    save.reset();
    setReviewing(true);
  };

  const onSave = (comment: string) =>
    save.mutate(
      { config: draftConfig, expectedVersion: baseline.source.version, comment },
      {
        onSuccess: (targeting) => {
          resetTo(targeting);
          setReviewing(false);
          notify('Changes saved');
        },
        onError: (error) => {
          if (error instanceof ApiError && error.status === 409) {
            setReviewing(false);
            setConflict(true);
            return;
          }

          const fieldErrors =
            error instanceof ApiError ? fromServerErrors(error.errors, 'config.') : noErrors;
          if (Object.keys(fieldErrors).length > 0) {
            setServerErrors(fieldErrors);
            setShowAllErrors(true);
            setReviewing(false);
          }
        },
      },
    );

  const loadLatest = async () => {
    setConflict(false);
    await queryClient.invalidateQueries({ queryKey: queryKeys.flag(projectKey, flag.key) });
    const latest = queryClient
      .getQueryData<Flag>(queryKeys.flag(projectKey, flag.key))
      ?.environments.find((item) => item.environmentKey === environment.key)?.config;
    if (latest) {
      resetTo(latest);
      notify('Loaded the latest version', 'info');
    }
  };

  const offVariation = variations.find((variation) => variation.id === draft.offVariationId);
  const reviewError =
    save.isError &&
    !(
      save.error instanceof ApiError &&
      (save.error.status === 409 ||
        Object.keys(fromServerErrors(save.error.errors, 'config.')).length > 0)
    )
      ? errorMessage(save.error)
      : null;
  const staleDraft = dirty && saved.version !== baseline.source.version;

  return (
    <Stack spacing={2.5}>
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        spacing={1.5}
        sx={{ alignItems: { sm: 'center' } }}
      >
        <Box sx={{ flexGrow: 1 }}>
          {blockedReason && <Alert severity="info">{blockedReason}</Alert>}
        </Box>
        <Button
          variant="outlined"
          startIcon={<ScienceOutlined />}
          onClick={() => setTesting(true)}
          sx={{ alignSelf: { xs: 'flex-start', sm: 'center' } }}
        >
          Test flag
        </Button>
      </Stack>

      <Section
        title="Status"
        highlighted={match?.reason.kind === 'OFF'}
        highlightLabel="Matched in test"
      >
        <Stack spacing={2}>
          <FormControlLabel
            control={
              <Switch
                checked={draft.enabled}
                disabled={disabled}
                onChange={(event) =>
                  dispatch({ type: 'setEnabled', enabled: event.target.checked })
                }
              />
            }
            label={
              draft.enabled
                ? 'Serving targeting rules'
                : `Off: serving ${offVariation?.name ?? 'the off variation'}`
            }
          />
          <TextField
            select
            label="When off, serve"
            value={offVariation ? draft.offVariationId : ''}
            disabled={disabled}
            onChange={(event) =>
              dispatch({ type: 'setOffVariation', variationId: event.target.value })
            }
            error={Boolean(errorAt(errors, 'offVariationId'))}
            helperText={errorAt(errors, 'offVariationId')}
            sx={{ maxWidth: 420 }}
          >
            {variations.map((variation, index) => (
              <MenuItem key={variation.id} value={variation.id}>
                <VariationLabel variation={variation} index={index} />
              </MenuItem>
            ))}
          </TextField>
        </Stack>
      </Section>

      <Section
        title="Individual targeting"
        description="These context keys get their variation before any rule is checked."
        highlighted={match?.reason.kind === 'TARGET_MATCH'}
        highlightLabel="Matched in test"
      >
        <IndividualTargeting
          targets={draft.targets}
          variations={variations}
          errors={errors}
          disabled={disabled}
          highlightedVariationId={match?.reason.kind === 'TARGET_MATCH' ? match.variationId : null}
          dispatch={dispatch}
        />
      </Section>

      <Section
        title="Rules"
        description="Rules are checked in order. The first rule whose conditions all match decides what is served."
      >
        <Stack spacing={2}>
          {errorAt(errors, 'rules') && <Alert severity="error">{errorAt(errors, 'rules')}</Alert>}
          {draft.rules.length === 0 && (
            <Typography color="text.secondary">
              No rules yet. Add a rule to serve a variation to contexts that match conditions.
            </Typography>
          )}
          {draft.rules.map((rule, index) => (
            <RulePanel
              key={rule.id}
              rule={rule}
              index={index}
              count={draft.rules.length}
              variations={variations}
              errors={errors}
              disabled={disabled}
              highlighted={match?.reason.kind === 'RULE_MATCH' && match.reason.ruleId === rule.id}
              dispatch={dispatch}
            />
          ))}
          <Box>
            <Button
              startIcon={<AddRounded />}
              disabled={disabled || draft.rules.length >= maxRules}
              onClick={() => dispatch(newRuleAction(variations))}
            >
              Add rule
            </Button>
          </Box>
        </Stack>
      </Section>

      <Section
        title="Default rule"
        description="Served when no individual target or rule matches."
        highlighted={match?.reason.kind === 'FALLTHROUGH'}
        highlightLabel="Matched in test"
      >
        <ServeEditor
          label="Default rule serves"
          serve={draft.fallthrough}
          onChange={(serve) =>
            dispatch({ type: 'setServe', location: { kind: 'fallthrough' }, serve })
          }
          variations={variations}
          errors={errors}
          path="fallthrough"
          disabled={disabled}
        />
      </Section>

      {dirty && (
        <Paper
          variant="outlined"
          role="region"
          aria-label="Unsaved changes"
          sx={{
            position: 'sticky',
            bottom: 16,
            zIndex: 2,
            p: 1.5,
            borderRadius: 2,
            borderColor: 'primary.main',
            display: 'flex',
            flexWrap: 'wrap',
            alignItems: 'center',
            gap: 1.5,
          }}
        >
          <Box sx={{ flexGrow: 1, minWidth: 200 }}>
            <Typography sx={{ fontWeight: 600 }}>Unsaved changes</Typography>
            {showAllErrors && problemCount > 0 && (
              <Typography variant="body2" color="error">
                Fix{' '}
                {problemCount === 1
                  ? 'the highlighted problem'
                  : `the ${problemCount} highlighted problems`}{' '}
                before reviewing.
              </Typography>
            )}
            {staleDraft && (
              <Typography variant="body2" color="text.secondary">
                Someone saved a newer version while you were editing.
              </Typography>
            )}
          </Box>
          <Button onClick={() => dispatch({ type: 'reset', draft: baseline.draft })}>
            Discard
          </Button>
          <Button variant="contained" onClick={review}>
            Review changes
          </Button>
        </Paper>
      )}

      <ReviewChangesDialog
        open={reviewing}
        environment={environment}
        saved={savedConfig}
        draft={draftConfig}
        pending={save.isPending}
        error={reviewError}
        onSave={onSave}
        onClose={() => setReviewing(false)}
      />
      <ConflictDialog
        open={conflict}
        draftJson={JSON.stringify(draftConfig, null, 2)}
        onLoadLatest={() => void loadLatest()}
        onClose={() => setConflict(false)}
      />
      <ConfirmDialog
        open={blocker.state === 'blocked'}
        title="Discard unsaved changes?"
        description={`Your targeting changes in ${environment.name} have not been saved. Leaving this page discards them.`}
        confirmLabel="Discard changes"
        cancelLabel="Keep editing"
        destructive
        onConfirm={() => blocker.proceed?.()}
        onClose={() => blocker.reset?.()}
      />
      <TestPanel
        open={testing}
        onClose={() => setTesting(false)}
        projectKey={projectKey}
        flagKey={flag.key}
        environment={environment}
        variations={variations}
        draftConfig={problemCount === 0 ? draftConfig : null}
        ruleDescriptions={draft.rules.map((rule) => rule.description.trim())}
        onResult={setMatch}
        onDraftErrors={(found) => {
          setServerErrors(found);
          setShowAllErrors(true);
        }}
      />
    </Stack>
  );
}
