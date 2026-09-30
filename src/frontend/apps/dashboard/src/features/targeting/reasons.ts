import type { EvaluationReason } from '../../api/types';

/** A sentence explaining why a value was served, for the test panel. */
export function describeReason(reason: EvaluationReason, ruleDescription?: string | null): string {
  switch (reason.kind) {
    case 'OFF':
      return 'The flag is off here, so it serves the off variation.';
    case 'TARGET_MATCH':
      return 'This context key is individually targeted.';
    case 'RULE_MATCH': {
      const rule = `rule ${(reason.ruleIndex ?? 0) + 1}${ruleDescription ? ` (${ruleDescription})` : ''}`;
      return reason.inRollout ? `Matched ${rule}, in a percentage rollout.` : `Matched ${rule}.`;
    }
    case 'FALLTHROUGH':
      return reason.inRollout
        ? 'No target or rule matched, so the default rule served a percentage rollout.'
        : 'No target or rule matched, so the default rule applied.';
    case 'FLAG_NOT_FOUND':
      return 'The flag was not found.';
    case 'ERROR':
      return 'The flag could not be evaluated.';
  }
}
