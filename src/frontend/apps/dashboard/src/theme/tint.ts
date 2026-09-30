import { alpha, type Theme } from '@mui/material/styles';

type ToneColor = 'primary' | 'success' | 'warning' | 'error';

/** A translucent tint of a palette color that follows the active color scheme (CSS variables). */
export function tint(theme: Theme, color: ToneColor, opacity: number): string {
  const channel = theme.vars?.palette[color].mainChannel;
  return channel ? `rgba(${channel} / ${opacity})` : alpha(theme.palette[color].main, opacity);
}
