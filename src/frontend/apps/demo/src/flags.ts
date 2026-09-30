/** The flags the store reads, with the defaults it uses when FlagForge is unavailable. */
export interface StoreTheme {
  accent: string;
  rounded: boolean;
}

export type CheckoutColor = 'primary' | 'secondary' | 'success';

export const flagDefaults = {
  promoBanner: false,
  promoBannerText: 'Welcome to Acme Coffee',
  newProductLayout: false,
  checkoutButtonColor: 'primary' as CheckoutColor,
  maxCartItems: 5,
  storeTheme: { accent: '#5E7F4F', rounded: true } as StoreTheme,
};

export const flagKeys = {
  promoBanner: 'promo-banner',
  promoBannerText: 'promo-banner-text',
  newProductLayout: 'new-product-layout',
  checkoutButtonColor: 'checkout-button-color',
  maxCartItems: 'max-cart-items',
  storeTheme: 'store-theme',
} as const;

// The SDK checks JSON types (string, number, ...); these guards check what the store needs beyond that, so an
// unexpected value falls back to the default instead of breaking the page.

export function checkoutColorOf(value: string): CheckoutColor {
  return value === 'secondary' || value === 'success' ? value : 'primary';
}

export function cartLimitOf(value: number): number {
  return Number.isInteger(value) && value >= 1 && value <= 100 ? value : flagDefaults.maxCartItems;
}

export function storeThemeOf(value: unknown): StoreTheme {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    return flagDefaults.storeTheme;
  }

  const { accent, rounded } = value as Record<string, unknown>;
  return {
    accent:
      typeof accent === 'string' && /^#[0-9a-fA-F]{6}$/.test(accent)
        ? accent
        : flagDefaults.storeTheme.accent,
    rounded: typeof rounded === 'boolean' ? rounded : flagDefaults.storeTheme.rounded,
  };
}
