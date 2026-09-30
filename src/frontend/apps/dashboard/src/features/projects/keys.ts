/** The key format shared by projects, environments, and flags (mirrors the server's KeyFormat). */
export const keyPattern = /^[a-z0-9][a-z0-9._-]{0,63}$/;

export const keyRequirement =
  "Keys start with a lowercase letter or digit and use only lowercase letters, digits, '.', '_', and '-' (at most 64 characters).";

/** Suggests a key from a name: "New checkout flow" becomes "new-checkout-flow". */
export function suggestKey(name: string): string {
  return name
    .toLowerCase()
    .normalize('NFKD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 64);
}
