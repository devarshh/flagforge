import type { FlagDetail, FlagEvaluation } from './types.js';

const notFound: FlagDetail<never>['reason'] = Object.freeze({ kind: 'FLAG_NOT_FOUND' });
const wrongType: FlagDetail<never>['reason'] = Object.freeze({ kind: 'ERROR' });

/**
 * Turns an evaluation into a typed result. The expected type comes from the default: a boolean default accepts only
 * booleans, a string only strings, a number only numbers, and an object or array any JSON object or array. A null
 * or undefined default accepts any value. Anything else falls back to the default.
 */
export function resolveDetail<T>(
  evaluation: FlagEvaluation | undefined,
  defaultValue: T,
): FlagDetail<T> {
  if (!evaluation || evaluation.variationId === undefined || evaluation.value === null) {
    return { value: defaultValue, reason: evaluation?.reason ?? notFound };
  }

  if (!matchesType(evaluation.value, defaultValue)) {
    return { value: defaultValue, reason: wrongType };
  }

  return {
    value: evaluation.value as T,
    variationId: evaluation.variationId,
    reason: evaluation.reason,
  };
}

function matchesType(value: unknown, defaultValue: unknown): boolean {
  if (defaultValue === null || defaultValue === undefined) {
    return true;
  }

  if (typeof defaultValue === 'object') {
    return typeof value === 'object' && value !== null;
  }

  return typeof value === typeof defaultValue;
}
