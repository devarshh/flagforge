export interface Product {
  id: string;
  name: string;
  origin: string;
  notes: string;
  /** Price in cents. */
  price: number;
  /** Colors for the drawn bag: body, label, and bean. */
  art: { bag: string; label: string; bean: string };
}

export const products: readonly Product[] = [
  {
    id: 'espresso-roast',
    name: 'Espresso Roast',
    origin: 'Brazil and Sumatra',
    notes: 'Dark chocolate, molasses',
    price: 1600,
    art: { bag: '#3B2A20', label: '#D9A441', bean: '#5C3A21' },
  },
  {
    id: 'morning-blend',
    name: 'Morning Blend',
    origin: 'Guatemala',
    notes: 'Milk chocolate, almond',
    price: 1400,
    art: { bag: '#8C5A3C', label: '#F4E3C3', bean: '#6B4226' },
  },
  {
    id: 'yirgacheffe',
    name: 'Ethiopia Yirgacheffe',
    origin: 'Ethiopia',
    notes: 'Jasmine, lemon, bergamot',
    price: 1900,
    art: { bag: '#5E7F4F', label: '#F7F3EE', bean: '#7A5230' },
  },
  {
    id: 'huila',
    name: 'Colombia Huila',
    origin: 'Colombia',
    notes: 'Caramel, red apple',
    price: 1700,
    art: { bag: '#A0522D', label: '#FBE8D3', bean: '#6F4428' },
  },
  {
    id: 'decaf',
    name: 'Swiss Water Decaf',
    origin: 'Peru',
    notes: 'Cocoa, toasted nuts',
    price: 1500,
    art: { bag: '#4A5D6E', label: '#E9EEF2', bean: '#5A3B26' },
  },
  {
    id: 'cold-brew',
    name: 'Cold Brew Blend',
    origin: 'Honduras and Kenya',
    notes: 'Brown sugar, cherry',
    price: 1800,
    art: { bag: '#2F4858', label: '#86BBD8', bean: '#4E3524' },
  },
];

export function productById(id: string): Product | undefined {
  return products.find((product) => product.id === id);
}

const currency = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' });

export function formatPrice(cents: number): string {
  return currency.format(cents / 100);
}
