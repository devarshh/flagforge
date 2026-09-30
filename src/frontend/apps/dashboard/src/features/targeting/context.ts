import type { JsonValue } from '../../api/types';

export type ParsedContext = { ok: true; context: JsonValue } | { ok: false; error: string };

/** Checks the test panel's JSON before sending it: an object with a string `key` and optional `attributes`. */
export function parseContext(text: string): ParsedContext {
  let value: unknown;
  try {
    value = JSON.parse(text);
  } catch {
    return { ok: false, error: 'This is not valid JSON. Check quotes, commas, and brackets.' };
  }

  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    return { ok: false, error: 'Write the context as a JSON object, such as {"key": "user-123"}.' };
  }

  const context = value as Record<string, unknown>;
  if (typeof context.key !== 'string' || context.key.length === 0 || context.key.length > 256) {
    return {
      ok: false,
      error: 'Give the context a "key": a string of 1 to 256 characters, such as "user-123".',
    };
  }

  if (
    context.attributes !== undefined &&
    (context.attributes === null ||
      typeof context.attributes !== 'object' ||
      Array.isArray(context.attributes))
  ) {
    return { ok: false, error: '"attributes" must be a JSON object, such as {"plan": "premium"}.' };
  }

  return { ok: true, context: context as JsonValue };
}

export interface ContextPreset {
  label: string;
  create: () => JsonValue;
}

/** The demo store's shoppers, plus a random visitor for trying percentage rollouts. */
export const contextPresets: readonly ContextPreset[] = [
  {
    label: 'Alice (enterprise, beta)',
    create: () => ({
      key: 'alice',
      attributes: { email: 'alice@acme.com', plan: 'enterprise', beta: true },
    }),
  },
  {
    label: 'Bob (free)',
    create: () => ({ key: 'bob', attributes: { email: 'bob@example.com', plan: 'free' } }),
  },
  {
    label: 'Carol (premium, early access)',
    create: () => ({
      key: 'carol',
      attributes: { email: 'carol@example.org', plan: 'premium', groups: ['early-access'] },
    }),
  },
  {
    label: 'Random visitor',
    create: () => ({ key: `visitor-${crypto.randomUUID().slice(0, 8)}` }),
  },
];
