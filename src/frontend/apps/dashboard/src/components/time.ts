const units: [Intl.RelativeTimeFormatUnit, number][] = [
  ['year', 365 * 24 * 3600],
  ['month', 30 * 24 * 3600],
  ['week', 7 * 24 * 3600],
  ['day', 24 * 3600],
  ['hour', 3600],
  ['minute', 60],
];

const relativeFormat = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });

/** "just now", "5 minutes ago", "in 2 days", ... */
export function formatRelative(date: Date, now: number): string {
  const seconds = Math.round((date.getTime() - now) / 1000);
  if (Math.abs(seconds) < 45) {
    return 'just now';
  }

  for (const [unit, size] of units) {
    if (Math.abs(seconds) >= size) {
      return relativeFormat.format(Math.round(seconds / size), unit);
    }
  }

  return relativeFormat.format(Math.round(seconds / 60), 'minute');
}

const hourMs = 3600 * 1000;

/**
 * For the start of an hourly usage bucket: "this hour" while it is the current hour (a precise "42 minutes ago" could
 * be off by up to an hour), otherwise the usual relative time.
 */
export function formatUsageHour(hourStart: Date, now: number): string {
  return now - hourStart.getTime() < hourMs ? 'this hour' : formatRelative(hourStart, now);
}

const absoluteFormat = new Intl.DateTimeFormat('en', { dateStyle: 'medium', timeStyle: 'short' });

export function formatDateTime(value: string | Date): string {
  return absoluteFormat.format(typeof value === 'string' ? new Date(value) : value);
}

/** "Sep 30, 2026, 9:00 – 10:00 PM" for the hour that starts at `hourStart`. */
export function formatHourRange(hourStart: Date): string {
  return absoluteFormat.formatRange(hourStart, new Date(hourStart.getTime() + hourMs));
}
