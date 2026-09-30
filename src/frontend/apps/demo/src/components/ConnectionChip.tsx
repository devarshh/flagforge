import { Chip, type ChipProps } from '@mui/material';
import { useConnectionState } from '@flagforge/sdk/react';
import type { ConnectionState } from '@flagforge/sdk';

const states: Record<ConnectionState, { label: string; color: ChipProps['color'] }> = {
  live: { label: 'Live', color: 'success' },
  polling: { label: 'Polling', color: 'warning' },
  connecting: { label: 'Connecting', color: 'default' },
  offline: { label: 'Offline', color: 'error' },
};

/** How flag values reach the page: a live WebSocket, polling, or nothing. */
export function ConnectionChip() {
  const state = states[useConnectionState()];
  return (
    <Chip
      size="small"
      color={state.color}
      label={state.label}
      aria-label={`Connection: ${state.label}`}
    />
  );
}
