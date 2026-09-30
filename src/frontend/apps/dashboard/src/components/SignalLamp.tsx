import { Box, ButtonBase, Tooltip } from '@mui/material';
import { tint } from '../theme/tint';

export interface SignalLampProps {
  on: boolean;
  /** Accessible name, for example "new-checkout in Production". */
  label: string;
  onToggle?: (next: boolean) => void;
  disabled?: boolean;
  /** Shown as a tooltip when disabled, so people learn why. */
  disabledReason?: string;
}

/**
 * A lit or unlit lamp that works as a switch. A row of them reads as a flag's state across environments. The lamp's
 * short transition is the only motion that happens without user action (a teammate's change arriving).
 */
export function SignalLamp({
  on,
  label,
  onToggle,
  disabled = false,
  disabledReason,
}: SignalLampProps) {
  const interactive = onToggle !== undefined && !disabled;
  const lamp = (
    <ButtonBase
      role="switch"
      aria-checked={on}
      aria-label={label}
      disabled={!interactive}
      onClick={() => onToggle?.(!on)}
      sx={(theme) => ({
        width: 40,
        height: 22,
        px: '3px',
        borderRadius: 11,
        border: '1.5px solid',
        borderColor: on ? 'success.main' : 'lamp.off',
        bgcolor: on ? tint(theme, 'success', 0.14) : 'transparent',
        justifyContent: on ? 'flex-end' : 'flex-start',
        transition: theme.transitions.create(['background-color', 'border-color'], {
          duration: 160,
        }),
        '&.Mui-disabled': { opacity: onToggle ? 0.55 : 1 },
        '&.Mui-focusVisible': {
          outline: `2px solid ${theme.vars?.palette.primary.main ?? theme.palette.primary.main}`,
          outlineOffset: 2,
        },
      })}
    >
      <Box
        aria-hidden
        sx={(theme) => ({
          width: 14,
          height: 14,
          borderRadius: '50%',
          border: '1.5px solid',
          borderColor: on ? 'success.main' : 'lamp.off',
          bgcolor: on ? 'success.main' : 'transparent',
          transition: theme.transitions.create(['background-color', 'border-color'], {
            duration: 160,
          }),
        })}
      />
    </ButtonBase>
  );

  if (disabled && disabledReason) {
    return (
      <Tooltip title={disabledReason}>
        <span>{lamp}</span>
      </Tooltip>
    );
  }

  return lamp;
}
