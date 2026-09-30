import { describe, expect, it } from 'vitest';
import { addItem, cartCount, cartTotal, overLimitBy, removeItem, type CartLine } from './cart';

describe('cart', () => {
  it('adds items up to the max-cart-items limit, then refuses more', () => {
    let lines: CartLine[] = [];
    lines = addItem(lines, 'espresso-roast', 3);
    lines = addItem(lines, 'espresso-roast', 3);
    lines = addItem(lines, 'decaf', 3);
    lines = addItem(lines, 'huila', 3);

    expect(lines).toEqual([
      { productId: 'espresso-roast', quantity: 2 },
      { productId: 'decaf', quantity: 1 },
    ]);
    expect(cartCount(lines)).toBe(3);
  });

  it('removes one at a time and drops empty lines', () => {
    const lines = removeItem(
      removeItem(
        [
          { productId: 'espresso-roast', quantity: 2 },
          { productId: 'decaf', quantity: 1 },
        ],
        'decaf',
      ),
      'espresso-roast',
    );
    expect(lines).toEqual([{ productId: 'espresso-roast', quantity: 1 }]);
  });

  it('reports how far over a lowered limit the cart is', () => {
    const lines = [{ productId: 'espresso-roast', quantity: 5 }];
    expect(overLimitBy(lines, 3)).toBe(2);
    expect(overLimitBy(lines, 10)).toBe(0);
  });

  it('totals prices in cents', () => {
    expect(
      cartTotal([
        { productId: 'espresso-roast', quantity: 2 },
        { productId: 'decaf', quantity: 1 },
      ]),
    ).toBe(1600 * 2 + 1500);
  });
});
