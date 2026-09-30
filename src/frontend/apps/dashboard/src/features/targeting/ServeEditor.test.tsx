import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useEffect, useState } from 'react';
import { describe, expect, it } from 'vitest';
import { renderWithProviders } from '../../test/render';
import { variations } from '../../test/fixtures';
import { createServe, toServe, type DraftServe } from './draft';
import { ServeEditor } from './ServeEditor';
import { validateServeDraft } from './validation';

let latest: DraftServe | null = null;
const record = (serve: DraftServe) => {
  latest = serve;
};

/** Holds the serve like the targeting tab does, with live validation, and reports every change. */
function Harness({ initial }: { initial: DraftServe }) {
  const [serve, setServe] = useState(initial);
  useEffect(() => record(serve), [serve]);
  return (
    <ServeEditor
      label="Default rule serves"
      serve={serve}
      onChange={setServe}
      variations={variations}
      errors={validateServeDraft(serve, 'fallthrough')}
      path="fallthrough"
    />
  );
}

describe('ServeEditor', () => {
  it('switches to a rollout that starts at 100% of the current variation', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Harness initial={createServe({ variationId: 'v_treat1' }, variations)} />);

    await user.click(screen.getByRole('radio', { name: 'A percentage rollout' }));

    expect(screen.getByRole('textbox', { name: 'Percentage for Control' })).toHaveValue('0');
    expect(screen.getByRole('textbox', { name: 'Percentage for Treatment' })).toHaveValue('100');
    expect(screen.getByText('Total 100%')).toBeInTheDocument();
    expect(toServe(latest!)).toEqual({
      rollout: { bucketBy: 'key', weights: [{ variationId: 'v_treat1', weight: 100_000 }] },
    });
  });

  it('converts percentages to weights and explains totals other than 100%', async () => {
    const user = userEvent.setup();
    const initial: DraftServe = {
      ...createServe({ variationId: 'v_ctrl01' }, variations),
      mode: 'rollout',
    };
    renderWithProviders(<Harness initial={initial} />);

    const control = screen.getByRole('textbox', { name: 'Percentage for Control' });
    const treatment = screen.getByRole('textbox', { name: 'Percentage for Treatment' });
    await user.clear(control);
    await user.type(control, '60');
    await user.clear(treatment);
    await user.type(treatment, '30');

    expect(screen.getByRole('alert')).toHaveTextContent(
      'Weights add up to 90%. Make them add up to 100%.',
    );

    await user.clear(treatment);
    await user.type(treatment, '39.999');
    expect(screen.getByRole('alert')).toHaveTextContent(
      'Weights add up to 99.999%. Make them add up to 100%.',
    );

    await user.type(treatment, '9');
    expect(
      screen.getByText('Enter a percentage from 0 to 100, with up to three decimals.'),
    ).toBeInTheDocument();

    await user.clear(treatment);
    await user.type(treatment, '40');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(toServe(latest!)).toEqual({
      rollout: {
        bucketBy: 'key',
        weights: [
          { variationId: 'v_ctrl01', weight: 60_000 },
          { variationId: 'v_treat1', weight: 40_000 },
        ],
      },
    });
  });
});
