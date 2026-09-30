import { describe, expect, it } from 'vitest';
import { seriesByVariation, usageWindow } from './usage';

const now = Date.parse('2026-09-30T21:42:00Z');
const hour = (iso: string) => Date.parse(iso);

describe('usageWindow', () => {
  it('covers the last 24 hours with unique hourly buckets, oldest first, including the current hour', () => {
    const window = usageWindow('24h', now);

    expect(window.granularity).toBe('hour');
    expect(window.buckets).toHaveLength(25);
    expect(new Set(window.buckets).size).toBe(25);
    expect(window.buckets[0]).toBe(hour('2026-09-29T21:00:00Z'));
    expect(window.buckets.at(-1)).toBe(hour('2026-09-30T21:00:00Z'));
  });

  it('covers the last 30 days with daily buckets', () => {
    const window = usageWindow('30d', now);

    expect(window.granularity).toBe('day');
    expect(window.buckets[0]).toBe(hour('2026-08-31T00:00:00Z'));
    expect(window.buckets.at(-1)).toBe(hour('2026-09-30T00:00:00Z'));
  });
});

describe('seriesByVariation', () => {
  it('puts each count in its own bucket and fills the rest with zeros', () => {
    const { buckets } = usageWindow('24h', now);
    const series = seriesByVariation(
      [
        { bucketStart: '2026-09-29T21:00:00+00:00', variationId: 'true', count: 77 },
        { bucketStart: '2026-09-30T21:00:00+00:00', variationId: 'true', count: 6 },
        { bucketStart: '2026-09-30T21:00:00+00:00', variationId: 'false', count: 2 },
      ],
      buckets,
      ['true', 'false'],
    );

    const trueCounts = series.get('true')!;
    const falseCounts = series.get('false')!;
    expect(trueCounts[0]).toBe(77);
    expect(trueCounts.at(-1)).toBe(6);
    expect(falseCounts[0]).toBe(0);
    expect(falseCounts.at(-1)).toBe(2);
    expect(falseCounts.reduce((sum, count) => sum + count, 0)).toBe(2);
  });
});
