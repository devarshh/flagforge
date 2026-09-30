import {
  Alert,
  Autocomplete,
  Box,
  FormControlLabel,
  InputAdornment,
  MenuItem,
  Radio,
  RadioGroup,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import type { Variation } from '../../api/types';
import { withMono } from '../../components/mono';
import { variationColor } from '../../theme/tokens';
import type { DraftServe } from './draft';
import { attributeSuggestions } from './operators';
import { percentToWeight, totalOf, totalWeight, weightToPercent } from './rollout';
import { errorAt, errorUnder, type DraftErrors } from './validation';
import { VariationLabel } from './VariationLabel';

export interface ServeEditorProps {
  /** Names the group for assistive technology, for example "Rule 1 serves". */
  label: string;
  serve: DraftServe;
  onChange: (serve: DraftServe) => void;
  variations: readonly Variation[];
  errors: DraftErrors;
  /** The serve's path in the config, such as `rules[0].serve` or `fallthrough`. */
  path: string;
  disabled?: boolean;
}

/** Serve a single variation, or split traffic by percentage (weights are thousandths of a percent). */
export function ServeEditor({
  label,
  serve,
  onChange,
  variations,
  errors,
  path,
  disabled = false,
}: ServeEditorProps) {
  const serveError = errorAt(errors, path);
  return (
    <Box component="fieldset" sx={{ border: 0, p: 0, m: 0, minWidth: 0 }}>
      <legend className="visually-hidden">{label}</legend>
      <RadioGroup
        row
        value={serve.mode}
        onChange={(event) => onChange({ ...serve, mode: event.target.value as DraftServe['mode'] })}
      >
        <FormControlLabel
          value="variation"
          control={<Radio size="small" />}
          label="A variation"
          disabled={disabled}
        />
        <FormControlLabel
          value="rollout"
          control={<Radio size="small" />}
          label="A percentage rollout"
          disabled={disabled}
        />
      </RadioGroup>
      {serveError && (
        <Alert severity="error" sx={{ my: 1 }}>
          {serveError}
        </Alert>
      )}
      {serve.mode === 'variation' ? (
        <TextField
          select
          label="Variation"
          value={
            variations.some((variation) => variation.id === serve.variationId)
              ? serve.variationId
              : ''
          }
          disabled={disabled}
          onChange={(event) => onChange({ ...serve, variationId: event.target.value })}
          error={Boolean(errorAt(errors, `${path}.variationId`))}
          helperText={errorAt(errors, `${path}.variationId`)}
          sx={{ mt: 1, width: '100%', maxWidth: 420 }}
        >
          {variations.map((variation, index) => (
            <MenuItem key={variation.id} value={variation.id}>
              <VariationLabel variation={variation} index={index} />
            </MenuItem>
          ))}
        </TextField>
      ) : (
        <RolloutEditor
          serve={serve}
          onChange={onChange}
          variations={variations}
          errors={errors}
          path={`${path}.rollout`}
          disabled={disabled}
        />
      )}
    </Box>
  );
}

interface RolloutEditorProps {
  serve: DraftServe;
  onChange: (serve: DraftServe) => void;
  variations: readonly Variation[];
  errors: DraftErrors;
  path: string;
  disabled: boolean;
}

function RolloutEditor({
  serve,
  onChange,
  variations,
  errors,
  path,
  disabled,
}: RolloutEditorProps) {
  const total = totalOf(serve.percents.map((item) => item.percent));
  const totalError = errorAt(errors, `${path}.weights`);
  const nameOf = (variationId: string) =>
    variations.find((variation) => variation.id === variationId)?.name ?? variationId;
  const segments = serve.percents.map((item, index) => {
    const variationIndex = variations.findIndex((variation) => variation.id === item.variationId);
    return {
      variationId: item.variationId,
      weight: percentToWeight(item.percent) ?? 0,
      color: variationColor(variationIndex >= 0 ? variationIndex : index),
    };
  });

  const setPercent = (index: number, percent: string) =>
    onChange({
      ...serve,
      percents: serve.percents.map((item, i) => (i === index ? { ...item, percent } : item)),
    });

  return (
    <Stack spacing={1.5} sx={{ mt: 1 }}>
      <Box
        role="img"
        aria-label={`Rollout preview: ${segments.map((segment) => `${nameOf(segment.variationId)} ${weightToPercent(segment.weight)}%`).join(', ')}`}
        sx={{
          display: 'flex',
          height: 12,
          borderRadius: 1,
          overflow: 'hidden',
          bgcolor: 'action.hover',
          maxWidth: 560,
        }}
      >
        {segments.map((segment) => (
          <Box
            key={segment.variationId}
            sx={{
              width: `${Math.min(segment.weight, totalWeight) / 1000}%`,
              bgcolor: segment.color,
            }}
          />
        ))}
      </Box>
      <Stack spacing={1} sx={{ maxWidth: 560 }}>
        {serve.percents.map((item, index) => {
          const variationIndex = variations.findIndex(
            (variation) => variation.id === item.variationId,
          );
          const variation = variations[variationIndex];
          const fieldError = errorUnder(errors, `${path}.weights[${index}]`);
          return (
            <Stack
              key={item.variationId}
              direction="row"
              spacing={1.5}
              sx={{ alignItems: 'flex-start' }}
            >
              <Box sx={{ flex: 1, minWidth: 0, pt: 1 }}>
                {variation ? (
                  <VariationLabel variation={variation} index={variationIndex} />
                ) : (
                  <span className="mono">{item.variationId}</span>
                )}
              </Box>
              <TextField
                value={item.percent}
                disabled={disabled}
                onChange={(event) => setPercent(index, event.target.value)}
                error={Boolean(fieldError)}
                helperText={fieldError}
                sx={{ width: 132 }}
                slotProps={{
                  input: { endAdornment: <InputAdornment position="end">%</InputAdornment> },
                  htmlInput: {
                    inputMode: 'decimal',
                    'aria-label': `Percentage for ${nameOf(item.variationId)}`,
                  },
                }}
              />
            </Stack>
          );
        })}
      </Stack>
      <Typography
        variant="body2"
        color={total === totalWeight ? 'text.secondary' : 'error'}
        aria-live="polite"
      >
        Total {weightToPercent(total)}%
      </Typography>
      {totalError && (
        <Alert severity="error" sx={{ maxWidth: 560 }}>
          {totalError}
        </Alert>
      )}
      <Autocomplete
        freeSolo
        disableClearable
        options={attributeSuggestions}
        value={serve.bucketBy}
        inputValue={serve.bucketBy}
        disabled={disabled}
        onInputChange={(_, bucketBy) => onChange({ ...serve, bucketBy })}
        onChange={(_, bucketBy) => onChange({ ...serve, bucketBy })}
        sx={{ maxWidth: 280 }}
        renderInput={(params) => (
          <TextField
            {...params}
            label="Bucket by"
            error={Boolean(errorAt(errors, `${path}.bucketBy`))}
            helperText={
              errorAt(errors, `${path}.bucketBy`) ??
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
    </Stack>
  );
}
