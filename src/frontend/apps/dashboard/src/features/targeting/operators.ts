import type { ClauseOperator } from '../../api/types';

/** A human label for an operator plus `negate`, as shown in the condition editor. */
export interface OperatorOption {
  id: string;
  label: string;
  operator: ClauseOperator;
  negate: boolean;
  /** Offered in the menu; the others appear only when a saved condition already uses them. */
  offered: boolean;
}

const option = (
  operator: ClauseOperator,
  negate: boolean,
  label: string,
  offered = true,
): OperatorOption => ({
  id: negate ? `not-${operator}` : operator,
  label,
  operator,
  negate,
  offered,
});

export const operatorOptions: readonly OperatorOption[] = [
  option('in', false, 'is one of'),
  option('in', true, 'is not one of'),
  option('contains', false, 'contains'),
  option('contains', true, 'does not contain'),
  option('startsWith', false, 'starts with'),
  option('startsWith', true, 'does not start with', false),
  option('endsWith', false, 'ends with'),
  option('endsWith', true, 'does not end with', false),
  option('gt', false, 'is greater than'),
  option('gt', true, 'is not greater than', false),
  option('gte', false, 'is at least'),
  option('gte', true, 'is not at least', false),
  option('lt', false, 'is less than'),
  option('lt', true, 'is not less than', false),
  option('lte', false, 'is at most'),
  option('lte', true, 'is not at most', false),
  option('exists', false, 'exists'),
  option('exists', true, 'does not exist'),
];

export function operatorOptionFor(operator: ClauseOperator, negate: boolean): OperatorOption {
  return (
    operatorOptions.find((item) => item.operator === operator && item.negate === negate) ??
    operatorOptions[0]!
  );
}

export function operatorOptionById(id: string): OperatorOption | undefined {
  return operatorOptions.find((item) => item.id === id);
}

/** The options to show for a condition: every offered option, plus the current one if it is not offered. */
export function selectableOperatorOptions(current: OperatorOption): OperatorOption[] {
  return operatorOptions.filter((item) => item.offered || item.id === current.id);
}

export function isNumericOperator(operator: ClauseOperator): boolean {
  return operator === 'gt' || operator === 'gte' || operator === 'lt' || operator === 'lte';
}

/** Attributes suggested in the condition editor; any valid attribute name can be typed. */
export const attributeSuggestions = ['key', 'email', 'plan', 'country', 'beta', 'groups'];
