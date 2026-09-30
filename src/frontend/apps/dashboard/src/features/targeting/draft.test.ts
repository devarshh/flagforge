import { describe, expect, it } from 'vitest';
import {
  createClause,
  createDraft,
  duplicateRuleAction,
  newRuleAction,
  sameConfig,
  targetingReducer,
  toConfig,
  type DraftAction,
  type TargetingDraft,
} from './draft';
import { config, variations } from '../../test/fixtures';

function apply(draft: TargetingDraft, ...actions: DraftAction[]): TargetingDraft {
  return actions.reduce(targetingReducer, draft);
}

describe('createDraft', () => {
  const draft = createDraft(config, variations);

  it('keeps a target list for every variation, in variation order', () => {
    expect(draft.targets).toEqual([
      { variationId: 'v_ctrl01', contextKeys: [] },
      { variationId: 'v_treat1', contextKeys: ['alice', 'bob'] },
      { variationId: 'v_third1', contextKeys: [] },
    ]);
  });

  it('starts a fixed serve with 100% of its variation, ready to switch to a rollout', () => {
    expect(draft.rules[0]!.serve).toEqual({
      mode: 'variation',
      variationId: 'v_treat1',
      bucketBy: 'key',
      percents: [
        { variationId: 'v_ctrl01', percent: '0' },
        { variationId: 'v_treat1', percent: '100' },
        { variationId: 'v_third1', percent: '0' },
      ],
    });
  });

  it('shows rollout weights as percentages', () => {
    expect(draft.rules[1]!.serve.mode).toBe('rollout');
    expect(draft.rules[1]!.serve.percents.map((item) => item.percent)).toEqual(['25', '75', '0']);
  });

  it('gives every condition its own row id', () => {
    const uids = draft.rules.flatMap((rule) => rule.clauses.map((clause) => clause.uid));
    expect(new Set(uids).size).toBe(uids.length);
  });
});

describe('toConfig', () => {
  it('sends every target list and only non-zero rollout weights', () => {
    expect(toConfig(createDraft(config, variations))).toEqual({
      enabled: true,
      offVariationId: 'v_ctrl01',
      targets: [
        { variationId: 'v_ctrl01', contextKeys: [] },
        { variationId: 'v_treat1', contextKeys: ['alice', 'bob'] },
        { variationId: 'v_third1', contextKeys: [] },
      ],
      rules: [
        {
          id: 'r_paid01',
          description: 'Paid plans',
          clauses: [
            { attribute: 'plan', operator: 'in', values: ['premium', 'enterprise'], negate: false },
          ],
          serve: { variationId: 'v_treat1' },
        },
        {
          id: 'r_staff1',
          description: null,
          clauses: [
            { attribute: 'email', operator: 'endsWith', values: ['@acme.com'], negate: false },
          ],
          serve: {
            rollout: {
              bucketBy: 'key',
              weights: [
                { variationId: 'v_ctrl01', weight: 25_000 },
                { variationId: 'v_treat1', weight: 75_000 },
              ],
            },
          },
        },
      ],
      fallthrough: { variationId: 'v_ctrl01' },
    });
  });

  it('converts typed percentages to weights and trims text', () => {
    const draft = createDraft(config, variations);
    const next = apply(
      draft,
      {
        type: 'setServe',
        location: { kind: 'fallthrough' },
        serve: {
          mode: 'rollout',
          variationId: 'v_ctrl01',
          bucketBy: ' orgId ',
          percents: [
            { variationId: 'v_ctrl01', percent: '33.333' },
            { variationId: 'v_treat1', percent: '33.333' },
            { variationId: 'v_third1', percent: '33.334' },
          ],
        },
      },
      { type: 'setRuleDescription', ruleId: 'r_paid01', description: '   ' },
    );

    const saved = toConfig(next);
    expect(saved.fallthrough).toEqual({
      rollout: {
        bucketBy: 'orgId',
        weights: [
          { variationId: 'v_ctrl01', weight: 33_333 },
          { variationId: 'v_treat1', weight: 33_333 },
          { variationId: 'v_third1', weight: 33_334 },
        ],
      },
    });
    expect(saved.rules[0]!.description).toBeNull();
  });
});

describe('targetingReducer', () => {
  const draft = createDraft(config, variations);

  it('changes status, off variation, and target lists (dropping duplicate keys)', () => {
    const next = apply(
      draft,
      { type: 'setEnabled', enabled: false },
      { type: 'setOffVariation', variationId: 'v_third1' },
      { type: 'setTargetKeys', variationId: 'v_third1', contextKeys: ['carol', 'carol', 'dave'] },
    );

    expect(next.enabled).toBe(false);
    expect(next.offVariationId).toBe('v_third1');
    expect(next.targets[2]).toEqual({ variationId: 'v_third1', contextKeys: ['carol', 'dave'] });
    expect(draft.enabled).toBe(true);
  });

  it('adds a rule with one empty condition that serves the first variation', () => {
    const next = apply(draft, newRuleAction(variations));

    const added = next.rules[2]!;
    expect(next.rules).toHaveLength(3);
    expect(added.id).toMatch(/^r_[a-z0-9]{6}$/);
    expect(added.clauses).toHaveLength(1);
    expect(added.clauses[0]).toMatchObject({
      attribute: '',
      operator: 'in',
      negate: false,
      values: [],
    });
    expect(added.serve).toMatchObject({ mode: 'variation', variationId: 'v_ctrl01' });
  });

  it('duplicates a rule right after the original, with new ids and independent values', () => {
    const next = apply(draft, duplicateRuleAction(draft.rules[0]!));
    const [original, copy] = next.rules;

    expect(next.rules.map((rule) => rule.id)).toEqual(['r_paid01', copy!.id, 'r_staff1']);
    expect(copy!.id).not.toBe('r_paid01');
    expect(copy!.clauses[0]!.uid).not.toBe(original!.clauses[0]!.uid);
    expect(copy!.clauses[0]!.values).toEqual(original!.clauses[0]!.values);
    expect(copy!.clauses[0]!.values).not.toBe(original!.clauses[0]!.values);
  });

  it('moves rules up and down, and ignores moves past either end', () => {
    const moved = apply(draft, { type: 'moveRule', ruleId: 'r_staff1', offset: -1 });
    expect(moved.rules.map((rule) => rule.id)).toEqual(['r_staff1', 'r_paid01']);

    expect(targetingReducer(draft, { type: 'moveRule', ruleId: 'r_paid01', offset: -1 })).toBe(
      draft,
    );
    expect(targetingReducer(draft, { type: 'moveRule', ruleId: 'r_staff1', offset: 1 })).toBe(
      draft,
    );
  });

  it('removes a rule', () => {
    expect(
      apply(draft, { type: 'removeRule', ruleId: 'r_paid01' }).rules.map((rule) => rule.id),
    ).toEqual(['r_staff1']);
  });

  it('adds, updates, and removes conditions; "exists" clears the values', () => {
    const clause = createClause();
    const withClause = apply(draft, { type: 'addClause', ruleId: 'r_paid01', clause });
    expect(withClause.rules[0]!.clauses).toHaveLength(2);

    const first = withClause.rules[0]!.clauses[0]!;
    const updated = apply(withClause, {
      type: 'updateClause',
      ruleId: 'r_paid01',
      uid: first.uid,
      changes: { operator: 'exists', negate: true },
    });
    expect(updated.rules[0]!.clauses[0]).toMatchObject({
      attribute: 'plan',
      operator: 'exists',
      negate: true,
      values: [],
    });

    const removed = apply(updated, { type: 'removeClause', ruleId: 'r_paid01', uid: clause.uid });
    expect(removed.rules[0]!.clauses.map((item) => item.uid)).toEqual([first.uid]);
  });

  it('sets the serve of one rule without touching the others', () => {
    const serve = { ...draft.rules[0]!.serve, variationId: 'v_third1' };
    const next = apply(draft, {
      type: 'setServe',
      location: { kind: 'rule', ruleId: 'r_paid01' },
      serve,
    });

    expect(next.rules[0]!.serve.variationId).toBe('v_third1');
    expect(next.rules[1]).toBe(draft.rules[1]);
    expect(next.fallthrough).toBe(draft.fallthrough);
  });

  it('resets to a given draft', () => {
    const changed = apply(draft, { type: 'setEnabled', enabled: false });
    expect(targetingReducer(changed, { type: 'reset', draft })).toBe(draft);
  });

  it('never mutates the previous draft', () => {
    const snapshot = structuredClone(draft);
    apply(
      draft,
      { type: 'setEnabled', enabled: false },
      { type: 'setTargetKeys', variationId: 'v_ctrl01', contextKeys: ['x'] },
      {
        type: 'updateClause',
        ruleId: 'r_paid01',
        uid: draft.rules[0]!.clauses[0]!.uid,
        changes: { values: ['free'] },
      },
      { type: 'removeRule', ruleId: 'r_staff1' },
    );
    expect(draft).toEqual(snapshot);
  });
});

describe('sameConfig', () => {
  it('tells an unchanged draft from a changed one', () => {
    const draft = createDraft(config, variations);
    const saved = toConfig(draft);

    expect(sameConfig(saved, toConfig(createDraft(config, variations)))).toBe(true);
    expect(
      sameConfig(saved, toConfig(targetingReducer(draft, { type: 'setEnabled', enabled: false }))),
    ).toBe(false);
  });
});
