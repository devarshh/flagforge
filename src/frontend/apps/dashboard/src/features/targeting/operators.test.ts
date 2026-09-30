import { describe, expect, it } from 'vitest';
import type { ClauseOperator } from '../../api/types';
import {
  operatorOptionById,
  operatorOptionFor,
  operatorOptions,
  selectableOperatorOptions,
} from './operators';

describe('operator labels', () => {
  it.each<[string, ClauseOperator, boolean]>([
    ['is one of', 'in', false],
    ['is not one of', 'in', true],
    ['contains', 'contains', false],
    ['does not contain', 'contains', true],
    ['starts with', 'startsWith', false],
    ['ends with', 'endsWith', false],
    ['is greater than', 'gt', false],
    ['is at least', 'gte', false],
    ['is less than', 'lt', false],
    ['is at most', 'lte', false],
    ['exists', 'exists', false],
    ['does not exist', 'exists', true],
  ])('"%s" maps to %s with negate %s, and back', (label, operator, negate) => {
    const option = operatorOptions.find((item) => item.label === label);
    expect(option).toMatchObject({ operator, negate, offered: true });
    expect(operatorOptionFor(operator, negate).label).toBe(label);
    expect(operatorOptionById(option!.id)).toBe(option);
  });

  it('offers exactly the twelve labelled operators, in order', () => {
    expect(
      operatorOptions.filter((option) => option.offered).map((option) => option.label),
    ).toEqual([
      'is one of',
      'is not one of',
      'contains',
      'does not contain',
      'starts with',
      'ends with',
      'is greater than',
      'is at least',
      'is less than',
      'is at most',
      'exists',
      'does not exist',
    ]);
  });

  it('still shows a negated operator saved through the API, without offering it elsewhere', () => {
    const saved = operatorOptionFor('startsWith', true);
    expect(saved.label).toBe('does not start with');
    expect(selectableOperatorOptions(saved)).toContain(saved);
    expect(selectableOperatorOptions(operatorOptionFor('in', false))).not.toContain(saved);
  });

  it('gives every operator and negate combination a distinct id', () => {
    const ids = operatorOptions.map((option) => option.id);
    expect(new Set(ids).size).toBe(ids.length);
    expect(operatorOptions).toHaveLength(18);
  });
});
