import { describe, expect, it } from 'vitest';
import { cartLimitOf, checkoutColorOf, flagDefaults, storeThemeOf } from './flags';
import { describeReason, formatValue } from './reason';

describe('flag value guards', () => {
  it('accepts only the checkout colors the store knows', () => {
    expect(checkoutColorOf('success')).toBe('success');
    expect(checkoutColorOf('secondary')).toBe('secondary');
    expect(checkoutColorOf('fuchsia')).toBe('primary');
  });

  it('keeps cart limits sensible', () => {
    expect(cartLimitOf(10)).toBe(10);
    expect(cartLimitOf(0)).toBe(flagDefaults.maxCartItems);
    expect(cartLimitOf(2.5)).toBe(flagDefaults.maxCartItems);
  });

  it('fills in missing or malformed theme fields from the default', () => {
    expect(storeThemeOf({ accent: '#A0522D', rounded: false })).toEqual({
      accent: '#A0522D',
      rounded: false,
    });
    expect(storeThemeOf({ accent: 'red' })).toEqual(flagDefaults.storeTheme);
    expect(storeThemeOf(['#A0522D'])).toEqual(flagDefaults.storeTheme);
  });
});

describe('describeReason', () => {
  it('explains each reason in a few words', () => {
    expect(describeReason({ kind: 'RULE_MATCH', ruleIndex: 1, inRollout: true })).toBe(
      'Matched rule 2, in rollout',
    );
    expect(describeReason({ kind: 'RULE_MATCH', ruleIndex: 0 })).toBe('Matched rule 1');
    expect(describeReason({ kind: 'FALLTHROUGH', inRollout: true })).toBe(
      'Default rule, in rollout',
    );
    expect(describeReason({ kind: 'TARGET_MATCH' })).toBe('Targeted individually');
    expect(describeReason({ kind: 'OFF' })).toBe('Flag is off');
    expect(describeReason({ kind: 'FLAG_NOT_FOUND' })).toBe('Flag not found');
  });

  it('formats values the way JSON shows them', () => {
    expect(formatValue('primary')).toBe('"primary"');
    expect(formatValue({ accent: '#5E7F4F', rounded: true })).toBe(
      '{"accent":"#5E7F4F","rounded":true}',
    );
  });
});
