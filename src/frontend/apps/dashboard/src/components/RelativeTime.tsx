import { Tooltip } from '@mui/material';
import { formatDateTime, formatRelative } from './time';
import { useNow } from './useNow';

export interface RelativeTimeProps {
  value: string | null | undefined;
  /** Shown when there is no value, for example "Never". */
  fallback?: string;
}

export function RelativeTime({ value, fallback = 'Never' }: RelativeTimeProps) {
  const now = useNow();
  if (!value) {
    return <span>{fallback}</span>;
  }

  const date = new Date(value);
  return (
    <Tooltip title={formatDateTime(date)}>
      <time dateTime={value}>{formatRelative(date, now)}</time>
    </Tooltip>
  );
}
