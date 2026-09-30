import type { FieldErrors } from 'react-hook-form';
import { z } from 'zod';
import type { FlagType } from '../../api/types';
import {
  validateVariationRows,
  type VariationField,
  type VariationRow,
} from '../flags/variationValues';

export const variationRowSchema = z.object({
  uid: z.string(),
  id: z.string().optional(),
  name: z.string(),
  valueText: z.string(),
  description: z.string(),
});

/** Adds the variation rules as Zod issues at `variations.<index>.<field>` (or `variations.root`). */
export function addVariationIssues(
  ctx: z.RefinementCtx,
  type: FlagType,
  rows: VariationRow[],
): void {
  for (const error of validateVariationRows(type, rows)) {
    ctx.addIssue({
      code: 'custom',
      message: error.message,
      path:
        error.index === null
          ? ['variations', 'root']
          : ['variations', error.index, error.field ?? 'name'],
    });
  }
}

type RowErrors = FieldErrors<{ variations: VariationRow[] }>['variations'];

/** Reads one row's message from React Hook Form's nested errors. */
export function variationErrorReader(
  errors: RowErrors,
): (index: number, field: VariationField) => string | undefined {
  return (index, field) => {
    const row = errors?.[index];
    const key = field === 'value' ? 'valueText' : field;
    return row?.[key]?.message;
  };
}

/** Maps a server path such as `variations[1].value` to the form field `variations.1.valueText`. */
export function variationFieldName(
  path: string,
): `variations.${number}.${'name' | 'valueText' | 'description'}` | null {
  const match = /^variations\[(\d+)\]\.(name|value|description)$/.exec(path);
  if (!match) {
    return null;
  }

  const field = match[2] === 'value' ? 'valueText' : (match[2] as 'name' | 'description');
  return `variations.${Number(match[1])}.${field}`;
}
