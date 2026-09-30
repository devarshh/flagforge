import type { EvaluationContext } from '@flagforge/sdk';

export type PersonaId = 'alice' | 'bob' | 'carol' | 'random';

export interface Persona {
  id: PersonaId;
  name: string;
  description: string;
  /** A new context each call; the random visitor gets a new key every time. */
  context: () => EvaluationContext;
}

export const personas: readonly Persona[] = [
  {
    id: 'alice',
    name: 'Alice',
    description: 'Enterprise plan, beta tester, works at Acme',
    context: () => ({
      key: 'alice',
      attributes: { email: 'alice@acme.com', plan: 'enterprise', beta: true },
    }),
  },
  {
    id: 'bob',
    name: 'Bob',
    description: 'Free plan',
    context: () => ({ key: 'bob', attributes: { email: 'bob@example.com', plan: 'free' } }),
  },
  {
    id: 'carol',
    name: 'Carol',
    description: 'Premium plan, early access group',
    context: () => ({
      key: 'carol',
      attributes: { email: 'carol@example.org', plan: 'premium', groups: ['early-access'] },
    }),
  },
  {
    id: 'random',
    name: 'Random visitor',
    description: 'A new anonymous visitor each time, to see percentage rollouts at work',
    context: () => ({ key: `visitor-${crypto.randomUUID().slice(0, 8)}` }),
  },
];

export function personaById(id: PersonaId): Persona {
  return personas.find((persona) => persona.id === id) ?? personas[0]!;
}
