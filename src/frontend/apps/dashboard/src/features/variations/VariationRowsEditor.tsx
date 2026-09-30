import { Alert, Box, Button, IconButton, Stack, TextField, Tooltip } from '@mui/material';
import AddRounded from '@mui/icons-material/AddRounded';
import DeleteOutlineRounded from '@mui/icons-material/DeleteOutlineRounded';
import type { FlagType } from '../../api/types';
import {
  maxVariations,
  newRowUid,
  type VariationField,
  type VariationRow,
} from '../flags/variationValues';

export interface VariationRowsEditorProps {
  type: FlagType;
  rows: readonly VariationRow[];
  onChange: (rows: VariationRow[]) => void;
  errorFor: (index: number, field: VariationField) => string | undefined;
  listError?: string;
  disabled?: boolean;
  /** Why a row cannot be removed (for example, it is still served), or null when it can. */
  removeBlockedReason?: (row: VariationRow) => string | null;
}

const valuePlaceholder: Record<FlagType, string> = {
  boolean: '',
  string: 'Text served to your app',
  number: 'For example 10 or 2.5',
  json: '{ "key": "value" }',
};

/** Name, typed value, and description for each variation, with add and remove. */
export function VariationRowsEditor({
  type,
  rows,
  onChange,
  errorFor,
  listError,
  disabled = false,
  removeBlockedReason,
}: VariationRowsEditorProps) {
  const update = (index: number, changes: Partial<VariationRow>) =>
    onChange(rows.map((row, i) => (i === index ? { ...row, ...changes } : row)));

  return (
    <Stack spacing={1.5}>
      {listError && <Alert severity="error">{listError}</Alert>}
      {rows.map((row, index) => {
        const blocked =
          rows.length <= 2
            ? 'A flag needs at least two variations.'
            : (removeBlockedReason?.(row) ?? null);
        return (
          <Box
            key={row.uid}
            role="group"
            aria-label={`Variation ${index + 1}`}
            sx={{
              display: 'grid',
              gap: 1.5,
              gridTemplateColumns: {
                xs: '1fr',
                md: 'minmax(140px, 1fr) minmax(180px, 1.6fr) minmax(140px, 1fr) auto',
              },
              alignItems: 'start',
            }}
          >
            <TextField
              label="Name"
              value={row.name}
              disabled={disabled}
              onChange={(event) => update(index, { name: event.target.value })}
              error={Boolean(errorFor(index, 'name'))}
              helperText={errorFor(index, 'name')}
            />
            <TextField
              label="Value"
              value={row.valueText}
              disabled={disabled}
              placeholder={valuePlaceholder[type]}
              multiline={type === 'json'}
              minRows={type === 'json' ? 3 : undefined}
              onChange={(event) => update(index, { valueText: event.target.value })}
              error={Boolean(errorFor(index, 'value'))}
              helperText={errorFor(index, 'value')}
              slotProps={{
                htmlInput: {
                  className: 'mono',
                  spellCheck: false,
                  inputMode: type === 'number' ? 'decimal' : undefined,
                },
              }}
            />
            <TextField
              label="Description"
              value={row.description}
              disabled={disabled}
              onChange={(event) => update(index, { description: event.target.value })}
              error={Boolean(errorFor(index, 'description'))}
              helperText={errorFor(index, 'description')}
            />
            <Tooltip title={blocked ?? `Remove ${row.name || 'this variation'}`}>
              <span>
                <IconButton
                  aria-label={`Remove variation ${row.name || index + 1}`}
                  disabled={disabled || blocked !== null}
                  onClick={() => onChange(rows.filter((_, i) => i !== index))}
                  sx={{ mt: 0.5 }}
                >
                  <DeleteOutlineRounded />
                </IconButton>
              </span>
            </Tooltip>
          </Box>
        );
      })}
      <Box>
        <Button
          startIcon={<AddRounded />}
          disabled={disabled || rows.length >= maxVariations}
          onClick={() =>
            onChange([
              ...rows,
              {
                uid: newRowUid(),
                name: '',
                valueText: type === 'json' ? '{}' : '',
                description: '',
              },
            ])
          }
        >
          Add variation
        </Button>
      </Box>
    </Stack>
  );
}
