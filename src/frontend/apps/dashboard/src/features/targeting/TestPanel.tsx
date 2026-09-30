import {
  Alert,
  Box,
  Button,
  Chip,
  Divider,
  Drawer,
  IconButton,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import CloseRounded from '@mui/icons-material/CloseRounded';
import { useState } from 'react';
import { ApiError, errorMessage } from '../../api/errors';
import type { Environment, EvaluationResult, TargetingConfig, Variation } from '../../api/types';
import { EnvironmentChip } from '../../components/EnvironmentChip';
import { displayValue } from '../flags/variationValues';
import { contextPresets, parseContext } from './context';
import { describeReason } from './reasons';
import { usePreviewEvaluation } from './targetingApi';
import { fromServerErrors, type DraftErrors } from './validation';

export interface TestPanelProps {
  open: boolean;
  onClose: () => void;
  projectKey: string;
  flagKey: string;
  environment: Environment;
  variations: readonly Variation[];
  /** The draft as it would be saved, or null while it has problems. */
  draftConfig: TargetingConfig | null;
  /** Rule descriptions in order, to name the matching rule. */
  ruleDescriptions: readonly string[];
  onResult: (result: EvaluationResult | null) => void;
  /** Problems the server found in the draft (paths relative to the config). */
  onDraftErrors: (errors: DraftErrors) => void;
}

const initialContext = JSON.stringify(contextPresets[0]!.create(), null, 2);

/** Evaluates a context against the draft on the server (not counted as usage) and explains the result. */
export function TestPanel({
  open,
  onClose,
  projectKey,
  flagKey,
  environment,
  variations,
  draftConfig,
  ruleDescriptions,
  onResult,
  onDraftErrors,
}: TestPanelProps) {
  const preview = usePreviewEvaluation(projectKey, flagKey, environment.key);
  const [text, setText] = useState(initialContext);
  const [result, setResult] = useState<EvaluationResult | null>(null);
  const parsed = parseContext(text);
  const contextError =
    (!parsed.ok ? parsed.error : null) ??
    (preview.error instanceof ApiError
      ? Object.entries(preview.error.errors).find(([path]) => path.startsWith('context'))?.[1][0]
      : undefined);

  const evaluate = () => {
    if (!parsed.ok || draftConfig === null) {
      return;
    }

    preview.mutate(
      { context: parsed.context, draftConfig },
      {
        onSuccess: (evaluation) => {
          setResult(evaluation);
          onResult(evaluation);
        },
        onError: (error) => {
          setResult(null);
          onResult(null);
          if (error instanceof ApiError) {
            onDraftErrors(fromServerErrors(error.errors, 'draftConfig.'));
          }
        },
      },
    );
  };

  const variationIndex = result
    ? variations.findIndex((variation) => variation.id === result.variationId)
    : -1;
  const ruleIndex = result?.reason.ruleIndex;
  return (
    <Drawer
      anchor="right"
      open={open}
      onClose={onClose}
      keepMounted
      sx={{ zIndex: (theme) => theme.zIndex.modal }}
      slotProps={{ paper: { sx: { width: { xs: '100%', sm: 440 } } } }}
    >
      <Stack spacing={2} sx={{ p: 2.5 }} component="section" aria-labelledby="test-panel-title">
        <Stack direction="row" sx={{ alignItems: 'center' }}>
          <Typography id="test-panel-title" variant="h3" component="h2" sx={{ flexGrow: 1 }}>
            Test this flag
          </Typography>
          <IconButton aria-label="Close test panel" onClick={onClose}>
            <CloseRounded />
          </IconButton>
        </Stack>
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
          <Typography variant="body2" color="text.secondary">
            Evaluates your draft in
          </Typography>
          <EnvironmentChip
            name={environment.name}
            color={environment.color}
            isProtected={environment.isProtected}
          />
        </Stack>
        <Box>
          <Typography variant="subtitle2" component="h3" sx={{ mb: 1 }}>
            Presets
          </Typography>
          <Stack direction="row" sx={{ gap: 0.75, flexWrap: 'wrap' }}>
            {contextPresets.map((preset) => (
              <Chip
                key={preset.label}
                label={preset.label}
                variant="outlined"
                onClick={() => setText(JSON.stringify(preset.create(), null, 2))}
              />
            ))}
          </Stack>
        </Box>
        <TextField
          label="Context (JSON)"
          value={text}
          onChange={(event) => setText(event.target.value)}
          multiline
          minRows={8}
          error={Boolean(contextError)}
          helperText={contextError ?? 'A "key" and optional "attributes", as your app sends them.'}
          slotProps={{ htmlInput: { className: 'mono', spellCheck: false } }}
        />
        {draftConfig === null && (
          <Alert severity="warning">Fix the problems in the draft before testing it.</Alert>
        )}
        {preview.isError && !contextError && (
          <Alert severity="error">{errorMessage(preview.error)}</Alert>
        )}
        <Box>
          <Button
            variant="contained"
            onClick={evaluate}
            disabled={!parsed.ok || draftConfig === null}
            loading={preview.isPending}
          >
            Evaluate draft
          </Button>
        </Box>
        {result && (
          <>
            <Divider />
            <Stack spacing={1.5} aria-live="polite">
              <Typography variant="subtitle2" component="h3">
                Result
              </Typography>
              <Box>
                <Typography variant="body2" color="text.secondary">
                  Value
                </Typography>
                <Typography className="mono" sx={{ wordBreak: 'break-word' }}>
                  {displayValue(result.value)}
                </Typography>
              </Box>
              <Box>
                <Typography variant="body2" color="text.secondary">
                  Variation
                </Typography>
                <Typography>{variations[variationIndex]?.name ?? 'None'}</Typography>
              </Box>
              <Box>
                <Typography variant="body2" color="text.secondary">
                  Reason
                </Typography>
                <Typography>
                  {describeReason(
                    result.reason,
                    ruleIndex === undefined ? null : ruleDescriptions[ruleIndex] || null,
                  )}
                </Typography>
                <Typography variant="body2" color="text.secondary" className="mono">
                  {result.reason.kind}
                </Typography>
              </Box>
            </Stack>
          </>
        )}
      </Stack>
    </Drawer>
  );
}
