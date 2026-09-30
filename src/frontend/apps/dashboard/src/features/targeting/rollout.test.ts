import { describe, expect, it } from 'vitest';
import {
  percentError,
  percentToWeight,
  rolloutTotalError,
  totalOf,
  weightToPercent,
} from './rollout';

describe('percentToWeight', () => {
  it.each([
    ['', 0],
    ['0', 0],
    ['5', 5_000],
    ['12.5', 12_500],
    ['33.333', 33_333],
    ['0.001', 1],
    [' 7 ', 7_000],
    ['99.999', 99_999],
    ['100', 100_000],
    ['100.000', 100_000],
  ])('converts %j to %i thousandths of a percent', (text, weight) => {
    expect(percentToWeight(text)).toBe(weight);
  });

  it.each(['100.001', '101', '-1', '1.2345', 'abc', '.5', '5%', '1e2', '50,5'])(
    'rejects %j',
    (text) => {
      expect(percentToWeight(text)).toBeNull();
      expect(percentError(text)).toBe(
        'Enter a percentage from 0 to 100, with up to three decimals.',
      );
    },
  );
});

describe('weightToPercent', () => {
  it.each([
    [0, '0'],
    [1, '0.001'],
    [12_500, '12.5'],
    [33_333, '33.333'],
    [50_000, '50'],
    [100_000, '100'],
  ])('shows %i as %j', (weight, text) => {
    expect(weightToPercent(weight)).toBe(text);
  });

  it('round-trips every percentage with three decimals', () => {
    for (const weight of [0, 1, 999, 1_000, 33_334, 66_667, 99_999, 100_000]) {
      expect(percentToWeight(weightToPercent(weight))).toBe(weight);
    }
  });
});

describe('rolloutTotalError', () => {
  it('explains a total that is not 100%', () => {
    expect(rolloutTotalError(['50', '40'])).toBe(
      'Weights add up to 90%. Make them add up to 100%.',
    );
    expect(rolloutTotalError(['60', '40.5'])).toBe(
      'Weights add up to 100.5%. Make them add up to 100%.',
    );
  });

  it('accepts totals of exactly 100%, including thirds and blanks', () => {
    expect(rolloutTotalError(['33.333', '33.333', '33.334'])).toBeNull();
    expect(rolloutTotalError(['100', ''])).toBeNull();
    expect(totalOf(['33.333', '33.333', '33.334'])).toBe(100_000);
  });

  it('leaves invalid entries to their own message', () => {
    expect(rolloutTotalError(['abc', '100'])).toBeNull();
  });
});
