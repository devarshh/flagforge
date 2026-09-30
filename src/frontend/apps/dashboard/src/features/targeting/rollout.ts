/** 100% in thousandths of a percent, the unit rollout weights use. */
export const totalWeight = 100_000;

const percentPattern = /^(\d{1,3})(?:\.(\d{1,3}))?$/;

/**
 * Converts a percentage typed by a person ("33.333") to a weight (33333). Blank means 0. Returns null for anything
 * that is not a number from 0 to 100 with at most three decimals. Integer arithmetic avoids floating-point drift.
 */
export function percentToWeight(text: string): number | null {
  const trimmed = text.trim();
  if (trimmed === '') {
    return 0;
  }

  const match = percentPattern.exec(trimmed);
  if (!match) {
    return null;
  }

  const weight = Number(match[1]) * 1000 + Number((match[2] ?? '').padEnd(3, '0'));
  return weight <= totalWeight ? weight : null;
}

/** 33333 becomes "33.333"; 50000 becomes "50". */
export function weightToPercent(weight: number): string {
  const whole = Math.trunc(weight / 1000);
  const fraction = String(weight % 1000)
    .padStart(3, '0')
    .replace(/0+$/, '');
  return fraction === '' ? String(whole) : `${whole}.${fraction}`;
}

export const percentFormatMessage = 'Enter a percentage from 0 to 100, with up to three decimals.';

export function percentError(text: string): string | null {
  return percentToWeight(text) === null ? percentFormatMessage : null;
}

/** The total of the valid percentages, in weight units. */
export function totalOf(percents: readonly string[]): number {
  return percents.reduce((sum, text) => sum + (percentToWeight(text) ?? 0), 0);
}

/**
 * "Weights add up to 90%. Make them add up to 100%." when every percentage is valid but the total is not 100%;
 * null otherwise (invalid entries get their own message).
 */
export function rolloutTotalError(percents: readonly string[]): string | null {
  if (percents.some((text) => percentToWeight(text) === null)) {
    return null;
  }

  const total = totalOf(percents);
  return total === totalWeight
    ? null
    : `Weights add up to ${weightToPercent(total)}%. Make them add up to 100%.`;
}
