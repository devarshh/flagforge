import type { FlagType, JsonValue, Variation, VariationInput } from '../../api/types';

export const flagTypeLabels: Record<FlagType, string> = {
  boolean: 'Boolean',
  string: 'String',
  number: 'Number',
  json: 'JSON',
};

export type ParsedValue = { ok: true; value: JsonValue } | { ok: false; error: string };

/** Text for editing a variation value: strings as typed, numbers as digits, JSON pretty-printed. */
export function valueToText(type: FlagType, value: JsonValue): string {
  if (type === 'string' && typeof value === 'string') {
    return value;
  }

  return type === 'json' ? JSON.stringify(value, null, 2) : JSON.stringify(value);
}

/** Parses an edited value with the same rules as the server (JSON flags hold an object or an array). */
export function parseValue(type: FlagType, text: string): ParsedValue {
  switch (type) {
    case 'boolean':
      return text === 'true' || text === 'false'
        ? { ok: true, value: text === 'true' }
        : { ok: false, error: 'Use true or false.' };
    case 'string':
      return { ok: true, value: text };
    case 'number': {
      const trimmed = text.trim();
      const value = Number(trimmed);
      return trimmed !== '' && Number.isFinite(value)
        ? { ok: true, value }
        : { ok: false, error: 'Enter a number, such as 10 or 2.5.' };
    }
    case 'json':
      try {
        const value = JSON.parse(text) as JsonValue;
        return value !== null && typeof value === 'object'
          ? { ok: true, value }
          : { ok: false, error: 'Enter a JSON object or array, such as {"limit": 10}.' };
      } catch {
        return { ok: false, error: 'This is not valid JSON. Check quotes, commas, and brackets.' };
      }
  }
}

/** A one-line rendering of a value for tables and selects. */
export function displayValue(value: JsonValue): string {
  return typeof value === 'string' ? `"${value}"` : JSON.stringify(value);
}

/** Deep equality for JSON values, ignoring object key order. */
export function jsonEqual(left: JsonValue, right: JsonValue): boolean {
  if (left === right) {
    return true;
  }

  if (Array.isArray(left) || Array.isArray(right)) {
    return (
      Array.isArray(left) &&
      Array.isArray(right) &&
      left.length === right.length &&
      left.every((item, index) => jsonEqual(item, right[index]!))
    );
  }

  if (left !== null && right !== null && typeof left === 'object' && typeof right === 'object') {
    const leftKeys = Object.keys(left);
    return (
      leftKeys.length === Object.keys(right).length &&
      leftKeys.every((key) => key in right && jsonEqual(left[key]!, right[key]!))
    );
  }

  return false;
}

/** One editable variation. `id` is set for variations that already exist; `uid` keys the row in lists. */
export interface VariationRow {
  uid: string;
  id?: string;
  name: string;
  valueText: string;
  description: string;
}

export type VariationField = 'name' | 'value' | 'description';

export interface VariationRowError {
  /** The row, or null for the list as a whole. */
  index: number | null;
  field: VariationField | null;
  message: string;
}

export const maxVariations = 20;

/** The server's variation rules (count, unique names and values, value matches the type), checked in the browser. */
export function validateVariationRows(
  type: FlagType,
  rows: readonly VariationRow[],
): VariationRowError[] {
  const errors: VariationRowError[] = [];
  if (rows.length < 2 || rows.length > maxVariations) {
    errors.push({ index: null, field: null, message: 'Add between 2 and 20 variations.' });
  }

  const names = new Set<string>();
  const values: JsonValue[] = [];
  rows.forEach((row, index) => {
    const name = row.name.trim();
    if (name === '' || name.length > 100) {
      errors.push({
        index,
        field: 'name',
        message: 'Name each variation (at most 100 characters).',
      });
    } else if (names.has(name.toLowerCase())) {
      errors.push({ index, field: 'name', message: 'Each variation needs a different name.' });
    } else {
      names.add(name.toLowerCase());
    }

    if (row.description.length > 500) {
      errors.push({
        index,
        field: 'description',
        message: 'Descriptions can be at most 500 characters.',
      });
    }

    const parsed = parseValue(type, row.valueText);
    if (!parsed.ok) {
      errors.push({ index, field: 'value', message: parsed.error });
    } else if (values.some((value) => jsonEqual(value, parsed.value))) {
      errors.push({ index, field: 'value', message: 'Each variation needs a different value.' });
    } else {
      values.push(parsed.value);
    }
  });
  return errors;
}

let rowCounter = 0;

export function newRowUid(): string {
  rowCounter += 1;
  return `row-${rowCounter}`;
}

export function toVariationRows(type: FlagType, variations: readonly Variation[]): VariationRow[] {
  return variations.map((variation) => ({
    uid: variation.id,
    id: variation.id,
    name: variation.name,
    valueText: valueToText(type, variation.value),
    description: variation.description ?? '',
  }));
}

/** Converts valid rows to the API shape; call only after `validateVariationRows` returned no errors. */
export function toVariationInputs(type: FlagType, rows: readonly VariationRow[]): VariationInput[] {
  return rows.map((row) => {
    const parsed = parseValue(type, row.valueText);
    if (!parsed.ok) {
      throw new Error(parsed.error);
    }

    return {
      id: row.id,
      name: row.name.trim(),
      value: parsed.value,
      description: row.description.trim() || null,
    };
  });
}

export function defaultVariationRows(type: FlagType): VariationRow[] {
  const row = (name: string, valueText: string): VariationRow => ({
    uid: newRowUid(),
    name,
    valueText,
    description: '',
  });
  switch (type) {
    case 'string':
      return [row('Control', 'control'), row('Treatment', 'treatment')];
    case 'number':
      return [row('Low', '0'), row('High', '1')];
    case 'json':
      return [row('Default', '{}'), row('Variant', '{\n  "enabled": true\n}')];
    case 'boolean':
      return [];
  }
}
