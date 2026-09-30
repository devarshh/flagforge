import { Autocomplete, Box, IconButton, MenuItem, TextField, Tooltip } from '@mui/material';
import CloseRounded from '@mui/icons-material/CloseRounded';
import { ChipInput } from '../../components/ChipInput';
import { withMono } from '../../components/mono';
import type { DraftClause } from './draft';
import {
  attributeSuggestions,
  isNumericOperator,
  operatorOptionById,
  operatorOptionFor,
  selectableOperatorOptions,
} from './operators';
import { errorAt, errorUnder, type DraftErrors } from './validation';

export interface ClauseRowProps {
  clause: DraftClause;
  /** Zero-based position, used in accessible names. */
  index: number;
  /** The condition's path in the config, such as `rules[0].clauses[1]`. */
  path: string;
  errors: DraftErrors;
  disabled: boolean;
  canRemove: boolean;
  onChange: (changes: Partial<Omit<DraftClause, 'uid'>>) => void;
  onRemove: () => void;
}

/** One condition: attribute, operator (a human label mapped to operator plus negate), and values. */
export function ClauseRow({
  clause,
  index,
  path,
  errors,
  disabled,
  canRemove,
  onChange,
  onRemove,
}: ClauseRowProps) {
  const current = operatorOptionFor(clause.operator, clause.negate);
  const attributeError = errorAt(errors, `${path}.attribute`);
  const operatorError = errorAt(errors, `${path}.operator`);
  const valuesError = errorUnder(errors, `${path}.values`);
  return (
    <Box
      role="group"
      aria-label={`Condition ${index + 1}`}
      sx={{
        display: 'grid',
        gap: 1.5,
        gridTemplateColumns: { xs: '1fr', md: '180px 190px minmax(200px, 1fr) auto' },
        alignItems: 'start',
      }}
    >
      <Autocomplete
        freeSolo
        disableClearable
        options={attributeSuggestions}
        value={clause.attribute}
        inputValue={clause.attribute}
        disabled={disabled}
        onInputChange={(_, attribute) => onChange({ attribute })}
        onChange={(_, attribute) => onChange({ attribute })}
        renderInput={(params) => (
          <TextField
            {...params}
            label="Attribute"
            error={Boolean(attributeError)}
            helperText={attributeError}
            slotProps={{
              ...params.slotProps,
              htmlInput: {
                ...params.slotProps.htmlInput,
                className: withMono(params.slotProps.htmlInput.className),
                spellCheck: false,
              },
            }}
          />
        )}
      />
      <TextField
        select
        label="Operator"
        value={current.id}
        disabled={disabled}
        onChange={(event) => {
          const selected = operatorOptionById(event.target.value);
          if (selected) {
            onChange({ operator: selected.operator, negate: selected.negate });
          }
        }}
        error={Boolean(operatorError)}
        helperText={operatorError}
      >
        {selectableOperatorOptions(current).map((option) => (
          <MenuItem key={option.id} value={option.id}>
            {option.label}
          </MenuItem>
        ))}
      </TextField>
      {clause.operator === 'exists' ? (
        <Box aria-hidden />
      ) : (
        <ChipInput
          label="Values"
          value={clause.values}
          onChange={(values) => onChange({ values })}
          disabled={disabled}
          mono
          placeholder={
            isNumericOperator(clause.operator) ? 'For example 18' : 'Type a value and press Enter'
          }
          error={Boolean(valuesError)}
          helperText={valuesError}
        />
      )}
      <Tooltip title={canRemove ? 'Remove condition' : 'A rule needs at least one condition'}>
        <span>
          <IconButton
            aria-label={`Remove condition ${index + 1}`}
            disabled={disabled || !canRemove}
            onClick={onRemove}
            sx={{ mt: 0.25 }}
          >
            <CloseRounded />
          </IconButton>
        </span>
      </Tooltip>
    </Box>
  );
}
