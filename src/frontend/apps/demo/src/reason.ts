import type { EvaluationReason, JsonValue } from '@flagforge/sdk';

/** "Matched rule 2, in rollout" and friends, for the flag inspector. */
export function describeReason(reason: EvaluationReason): string {
  const rollout = reason.inRollout ? ', in rollout' : '';
  switch (reason.kind) {
    case 'OFF':
      return 'Flag is off';
    case 'TARGET_MATCH':
      return 'Targeted individually';
    case 'RULE_MATCH':
      return `Matched rule ${(reason.ruleIndex ?? 0) + 1}${rollout}`;
    case 'FALLTHROUGH':
      return `Default rule${rollout}`;
    case 'FLAG_NOT_FOUND':
      return 'Flag not found';
    case 'ERROR':
      return 'Could not evaluate';
  }
}

export function formatValue(value: JsonValue): string {
  return typeof value === 'string' ? `"${value}"` : JSON.stringify(value);
}
