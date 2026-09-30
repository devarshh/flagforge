import type { ScheduledChange, ScheduledChangeStatus, Serve, Variation } from '../../api/types';
import type { Tone } from '../../components/ToneChip';
import { weightToPercent } from '../targeting/rollout';

export const statusLabels: Record<ScheduledChangeStatus, { label: string; tone: Tone }> = {
  pending: { label: 'Pending', tone: 'warning' },
  processing: { label: 'Running', tone: 'primary' },
  completed: { label: 'Completed', tone: 'success' },
  failed: { label: 'Failed', tone: 'error' },
  cancelled: { label: 'Cancelled', tone: 'neutral' },
};

export function describeServe(serve: Serve, variations: readonly Variation[]): string {
  const nameOf = (id: string) => variations.find((variation) => variation.id === id)?.name ?? id;
  if (serve.rollout) {
    return serve.rollout.weights
      .filter((weight) => weight.weight > 0)
      .map((weight) => `${nameOf(weight.variationId)} ${weightToPercent(weight.weight)}%`)
      .join(', ');
  }

  return serve.variationId ? nameOf(serve.variationId) : 'nothing';
}

/** "Turn on", "Turn off", or "Serve Treatment 25%, Control 75% by default". */
export function describeChange(
  change: Pick<ScheduledChange, 'action' | 'payload'>,
  variations: readonly Variation[],
): string {
  switch (change.action) {
    case 'turnOn':
      return 'Turn on';
    case 'turnOff':
      return 'Turn off';
    case 'setFallthrough':
      return change.payload
        ? `Default rule serves ${describeServe(change.payload, variations)}`
        : 'Change the default rule';
  }
}
