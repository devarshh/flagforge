import dayjs, { type Dayjs } from 'dayjs';
import { percentFormatMessage, percentToWeight } from '../targeting/rollout';

export interface Step {
  uid: number;
  executeAt: Dayjs | null;
  percent: string;
}

export const maxSteps = 10;

/** 5% in an hour, 25% in a day, 50% in two days, 100% in three days. */
export function defaultSteps(): Step[] {
  const now = dayjs().startOf('minute');
  return [
    { uid: 1, executeAt: now.add(1, 'hour'), percent: '5' },
    { uid: 2, executeAt: now.add(1, 'day'), percent: '25' },
    { uid: 3, executeAt: now.add(2, 'day'), percent: '50' },
    { uid: 4, executeAt: now.add(3, 'day'), percent: '100' },
  ];
}

/** Validates the steps; keys are `steps[i].executeAt` or `steps[i].percent`, like the server's paths. */
export function validateSteps(steps: readonly Step[], now: Dayjs): Record<string, string> {
  const errors: Record<string, string> = {};
  steps.forEach((step, index) => {
    if (step.executeAt === null || !step.executeAt.isValid()) {
      errors[`steps[${index}].executeAt`] = 'Choose a date and time.';
    } else if (step.executeAt.isBefore(now)) {
      errors[`steps[${index}].executeAt`] = 'Choose a time in the future.';
    } else {
      const previous = steps[index - 1]?.executeAt;
      if (previous && previous.isValid() && !step.executeAt.isAfter(previous)) {
        errors[`steps[${index}].executeAt`] = 'Each step must be later than the step before it.';
      }
    }

    if (percentToWeight(step.percent) === null) {
      errors[`steps[${index}].percent`] = percentFormatMessage;
    }
  });
  return errors;
}
