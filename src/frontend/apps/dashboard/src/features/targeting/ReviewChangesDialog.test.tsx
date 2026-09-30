import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../test/render';
import { config } from '../../test/fixtures';
import { ReviewChangesDialog } from './ReviewChangesDialog';

function renderDialog(isProtected: boolean) {
  const onSave = vi.fn();
  renderWithProviders(
    <ReviewChangesDialog
      open
      environment={{ name: isProtected ? 'Production' : 'Development', isProtected }}
      saved={config}
      draft={{ ...config, enabled: false }}
      pending={false}
      error={null}
      onSave={onSave}
      onClose={() => undefined}
    />,
  );
  return onSave;
}

describe('ReviewChangesDialog', () => {
  it('requires a comment before saving to a protected environment', async () => {
    const user = userEvent.setup();
    const onSave = renderDialog(true);
    const save = screen.getByRole('button', { name: 'Save changes' });
    const comment = screen.getByRole('textbox', { name: /Comment \(required\)/ });

    expect(save).toBeDisabled();
    await user.type(comment, '   ');
    expect(save).toBeDisabled();

    await user.clear(comment);
    await user.type(comment, '  Turning off for the incident  ');
    expect(save).toBeEnabled();
    await user.click(save);

    expect(onSave).toHaveBeenCalledWith('Turning off for the incident');
  });

  it('saves without a comment elsewhere', async () => {
    const user = userEvent.setup();
    const onSave = renderDialog(false);

    expect(screen.getByRole('textbox', { name: /Comment \(optional\)/ })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Save changes' }));

    expect(onSave).toHaveBeenCalledWith('');
  });

  it('shows what changes', () => {
    renderDialog(false);

    expect(screen.getByText('"enabled": true,')).toBeInTheDocument();
    expect(screen.getByText('"enabled": false,')).toBeInTheDocument();
  });
});
