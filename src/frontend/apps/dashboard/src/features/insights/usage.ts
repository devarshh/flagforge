import type { UsageBucket } from '../../api/types';

export type UsageRange = '24h' | '30d';

const hourMs = 3_600_000;
const dayMs = 86_400_000;

export interface UsageWindow {
  granularity: 'hour' | 'day';
  from: Date;
  to: Date;
  /** Bucket start times (UTC-aligned, like the server's buckets), oldest first. */
  buckets: number[];
}

/** The last 24 hours by hour, or the last 30 days by day, aligned the way the server buckets usage. */
export function usageWindow(range: UsageRange, now: number): UsageWindow {
  const hourly = range === '24h';
  const size = hourly ? hourMs : dayMs;
  const from = now - (hourly ? 24 * hourMs : 30 * dayMs);
  const buckets: number[] = [];
  for (let start = Math.floor(from / size) * size; start < now; start += size) {
    buckets.push(start);
  }

  return { granularity: hourly ? 'hour' : 'day', from: new Date(from), to: new Date(now), buckets };
}

/** Counts per variation per bucket, with zeros where nothing was evaluated. */
export function seriesByVariation(
  usage: readonly UsageBucket[],
  buckets: readonly number[],
  variationIds: readonly string[],
): Map<string, number[]> {
  const index = new Map(buckets.map((start, position) => [start, position]));
  const series = new Map(variationIds.map((id) => [id, buckets.map(() => 0)]));
  for (const bucket of usage) {
    const position = index.get(Date.parse(bucket.bucketStart));
    const counts = series.get(bucket.variationId) ?? buckets.map(() => 0);
    if (position !== undefined) {
      counts[position] = (counts[position] ?? 0) + bucket.count;
      series.set(bucket.variationId, counts);
    }
  }

  return series;
}
