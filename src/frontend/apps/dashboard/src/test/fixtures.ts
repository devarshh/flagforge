import type { TargetingConfig, Variation } from '../api/types';

export const variations: Variation[] = [
  { id: 'v_ctrl01', name: 'Control', value: 'control', description: null },
  { id: 'v_treat1', name: 'Treatment', value: 'treatment', description: null },
  { id: 'v_third1', name: 'Third', value: 'third', description: null },
];

export const config: TargetingConfig = {
  enabled: true,
  offVariationId: 'v_ctrl01',
  targets: [{ variationId: 'v_treat1', contextKeys: ['alice', 'bob'] }],
  rules: [
    {
      id: 'r_paid01',
      description: 'Paid plans',
      clauses: [
        { attribute: 'plan', operator: 'in', values: ['premium', 'enterprise'], negate: false },
      ],
      serve: { variationId: 'v_treat1', rollout: null },
    },
    {
      id: 'r_staff1',
      description: null,
      clauses: [{ attribute: 'email', operator: 'endsWith', values: ['@acme.com'], negate: false }],
      serve: {
        variationId: null,
        rollout: {
          bucketBy: 'key',
          weights: [
            { variationId: 'v_ctrl01', weight: 25_000 },
            { variationId: 'v_treat1', weight: 75_000 },
            { variationId: 'v_third1', weight: 0 },
          ],
        },
      },
    },
  ],
  fallthrough: { variationId: 'v_ctrl01', rollout: null },
};
