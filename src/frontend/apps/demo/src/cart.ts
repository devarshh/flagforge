import { productById } from './products';

export interface CartLine {
  productId: string;
  quantity: number;
}

export function cartCount(lines: readonly CartLine[]): number {
  return lines.reduce((sum, line) => sum + line.quantity, 0);
}

export function cartTotal(lines: readonly CartLine[]): number {
  return lines.reduce(
    (sum, line) => sum + (productById(line.productId)?.price ?? 0) * line.quantity,
    0,
  );
}

/** Adds one item unless the cart already holds `limit` items (the `max-cart-items` flag). */
export function addItem(lines: readonly CartLine[], productId: string, limit: number): CartLine[] {
  if (cartCount(lines) >= limit) {
    return [...lines];
  }

  const existing = lines.find((line) => line.productId === productId);
  return existing
    ? lines.map((line) =>
        line.productId === productId ? { ...line, quantity: line.quantity + 1 } : line,
      )
    : [...lines, { productId, quantity: 1 }];
}

export function removeItem(lines: readonly CartLine[], productId: string): CartLine[] {
  return lines
    .map((line) => (line.productId === productId ? { ...line, quantity: line.quantity - 1 } : line))
    .filter((line) => line.quantity > 0);
}

/** Items to remove before checkout, when the limit dropped below what the cart holds (a live flag change). */
export function overLimitBy(lines: readonly CartLine[], limit: number): number {
  return Math.max(0, cartCount(lines) - limit);
}
