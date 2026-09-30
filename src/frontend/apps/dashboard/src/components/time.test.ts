import { describe, expect, it } from 'vitest';
import { formatHourRange, formatRelative, formatUsageHour } from './time';

const now = Date.parse('2026-09-30T21:42:00Z');

describe('formatRelative', () => {
  it.each([
    ['2026-09-30T21:41:30Z', 'just now'],
    ['2026-09-30T21:37:00Z', '5 minutes ago'],
    ['2026-09-30T19:42:00Z', '2 hours ago'],
    ['2026-10-02T21:42:00Z', 'in 2 days'],
  ])('formats %s as "%s"', (value, expected) => {
    expect(formatRelative(new Date(value), now)).toBe(expected);
  });
});

describe('formatUsageHour', () => {
  it('says "this hour" for the current hour, however far into it we are', () => {
    expect(formatUsageHour(new Date('2026-09-30T21:00:00Z'), now)).toBe('this hour');
  });

  it('uses the relative time for earlier hours', () => {
    expect(formatUsageHour(new Date('2026-09-30T20:00:00Z'), now)).toBe('2 hours ago');
    expect(formatUsageHour(new Date('2026-09-27T21:00:00Z'), now)).toBe('3 days ago');
  });
});

describe('formatHourRange', () => {
  it('spans the whole hour', () => {
    const range = formatHourRange(new Date(2026, 8, 30, 21));
    expect(range).toContain('9:00');
    expect(range).toContain('10:00');
  });
});
