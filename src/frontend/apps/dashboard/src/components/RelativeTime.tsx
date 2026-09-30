import { Tooltip } from '@mui/material';
import { formatDateTime, formatHourRange, formatRelative, formatUsageHour } from './time';
import { useNow } from './useNow';

export interface RelativeTimeProps {
  value: string | null | undefined;
  /** Shown when there is no value, for example "Never". */
  fallback?: string;
  /** `hour` when the value is the start of an hourly usage bucket, such as a flag's last evaluation. */
  resolution?: 'exact' | 'hour';
}

export function RelativeTime({
  value,
  fallback = 'Never',
  resolution = 'exact',
}: RelativeTimeProps) {
  const now = useNow();
  if (!value) {
    return <span>{fallback}</span>;
  }

  const date = new Date(value);
  const hourly = resolution === 'hour';
  return (
    <Tooltip title={hourly ? formatHourRange(date) : formatDateTime(date)}>
      <time dateTime={value}>
        {hourly ? formatUsageHour(date, now) : formatRelative(date, now)}
      </time>
    </Tooltip>
  );
}
