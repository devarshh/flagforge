import { Box, Stack } from '@mui/material';
import type { Dispatch } from 'react';
import type { Variation } from '../../api/types';
import { ChipInput } from '../../components/ChipInput';
import type { DraftAction, DraftTarget } from './draft';
import { errorUnder, type DraftErrors } from './validation';
import { VariationLabel } from './VariationLabel';

export interface IndividualTargetingProps {
  targets: readonly DraftTarget[];
  variations: readonly Variation[];
  errors: DraftErrors;
  disabled: boolean;
  /** The variation whose list matched in the test panel. */
  highlightedVariationId: string | null;
  dispatch: Dispatch<DraftAction>;
}

/** Context keys that always get a specific variation, one list per variation. */
export function IndividualTargeting({
  targets,
  variations,
  errors,
  disabled,
  highlightedVariationId,
  dispatch,
}: IndividualTargetingProps) {
  return (
    <Stack spacing={2}>
      {targets.map((target, index) => {
        const variationIndex = variations.findIndex(
          (variation) => variation.id === target.variationId,
        );
        const variation = variations[variationIndex];
        const error = errorUnder(errors, `targets[${index}]`);
        return (
          <Box
            key={target.variationId}
            sx={{
              p: highlightedVariationId === target.variationId ? 1 : 0,
              borderRadius: 1,
              outline: highlightedVariationId === target.variationId ? '2px solid' : 'none',
              outlineColor: 'primary.main',
            }}
          >
            <Box sx={{ mb: 1 }}>
              {variation && <VariationLabel variation={variation} index={variationIndex} />}
            </Box>
            <ChipInput
              label={`Context keys served ${variation?.name ?? target.variationId}`}
              value={target.contextKeys}
              onChange={(contextKeys) =>
                dispatch({ type: 'setTargetKeys', variationId: target.variationId, contextKeys })
              }
              disabled={disabled}
              mono
              placeholder="user-123, user-456"
              error={Boolean(error)}
              helperText={error ?? 'Paste a comma- or newline-separated list to add many at once.'}
            />
          </Box>
        );
      })}
    </Stack>
  );
}
