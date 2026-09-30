import { createClient, type FlagForgeClient } from '@flagforge/sdk';
import { FlagForgeProvider } from '@flagforge/sdk/react';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { personas } from '../personas';
import { Storefront } from './Storefront';

type Flags = Record<
  string,
  {
    value: unknown;
    variationId: string;
    reason: { kind: string; ruleIndex?: number; inRollout?: boolean };
  }
>;

const alice = personas[0]!;
let client: FlagForgeClient | null = null;

afterEach(async () => {
  await client?.close();
  client = null;
});

/** Makes MUI's `lg` breakpoint match, where the flag inspector docks beside the store and starts open. */
function emulateWideScreen() {
  vi.spyOn(window, 'matchMedia').mockImplementation((query) => ({
    matches: query.includes('min-width:1200px'),
    media: query,
    onchange: null,
    addListener: () => undefined,
    removeListener: () => undefined,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
    dispatchEvent: () => false,
  }));
}

/** A real SDK client (polling, no WebSocket) reading from whatever `served` holds. */
function renderStore(initial: Flags) {
  const served = { current: initial };
  const requests: unknown[] = [];
  client = createClient({
    baseUrl: 'https://store.example.test',
    sdkKey: 'ffk_test',
    context: alice.context(),
    streaming: false,
    pollIntervalMs: 40,
    logger: {},
    fetch: (_input, init) => {
      requests.push(JSON.parse(typeof init?.body === 'string' ? init.body : '{}'));
      return Promise.resolve(
        new Response(JSON.stringify({ environmentVersion: 1, flags: served.current }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }),
      );
    },
  });
  render(
    <FlagForgeProvider client={client}>
      <Storefront
        persona={alice}
        context={alice.context()}
        onPersonaChange={() => undefined}
        onOpenSettings={() => undefined}
      />
    </FlagForgeProvider>,
  );
  return { served, requests };
}

const fallthrough = (value: unknown, variationId: string) => ({
  value,
  variationId,
  reason: { kind: 'FALLTHROUGH' },
});

describe('Storefront', () => {
  it('renders from defaults, then from served flags, and follows changes live', async () => {
    emulateWideScreen();
    const { served } = renderStore({
      'promo-banner': fallthrough(true, 'true'),
      'promo-banner-text': {
        value: 'Free shipping on every order this week',
        variationId: 'v_frship',
        reason: { kind: 'RULE_MATCH', ruleIndex: 0 },
      },
      'new-product-layout': fallthrough(false, 'false'),
      'max-cart-items': fallthrough(3, 'v_items3'),
    });

    expect(await screen.findByText('Free shipping on every order this week')).toBeInTheDocument();
    expect(
      screen.getByRole('list', { name: 'Coffees, list layout', hidden: true }),
    ).toBeInTheDocument();
    expect(screen.getByText(/Carts hold up to 3 bags/)).toBeInTheDocument();
    expect(screen.getAllByText('Matched rule 1').length).toBeGreaterThan(0);

    act(() => {
      served.current = {
        'promo-banner': fallthrough(false, 'false'),
        'new-product-layout': fallthrough(true, 'true'),
        'max-cart-items': fallthrough(10, 'v_item10'),
      };
    });

    await waitFor(() =>
      expect(screen.queryByText('Free shipping on every order this week')).not.toBeInTheDocument(),
    );
    expect(
      await screen.findByRole('list', { name: 'Coffees, grid layout', hidden: true }),
    ).toBeInTheDocument();
    expect(screen.getByText(/Carts hold up to 10 bags/)).toBeInTheDocument();
    const inspector = screen.getByRole('table', {
      name: 'Flags served to this shopper',
      hidden: true,
    });
    expect(within(inspector).getByText('new-product-layout').closest('tr')).toHaveAttribute(
      'data-changed',
      'true',
    );
  });

  it('stops adding to the cart at the max-cart-items limit', async () => {
    const user = userEvent.setup();
    renderStore({ 'max-cart-items': fallthrough(3, 'v_items3') });
    await screen.findByText(/Carts hold up to 3 bags/);

    // On a narrow screen the inspector would cover the store, so it starts closed.
    expect(
      screen.queryByRole('table', { name: 'Flags served to this shopper', hidden: true }),
    ).not.toBeInTheDocument();
    await screen.findByRole('button', { name: 'Add Espresso Roast to cart' });
    const add = () => screen.getByRole('button', { name: 'Add Espresso Roast to cart' });
    await user.click(add());
    await user.click(add());
    await user.click(add());

    expect(add()).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Cart, 3 items' })).toBeInTheDocument();
  });
});
