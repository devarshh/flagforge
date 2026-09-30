import { describe, expect, it } from 'vitest';
import { createDraft, targetingReducer, type DraftAction } from './draft';
import { config, variations } from '../../test/fixtures';
import { errorUnder, fromServerErrors, liveErrors, validateDraft } from './validation';

const draft = createDraft(config, variations);
const edit = (...actions: DraftAction[]) => actions.reduce(targetingReducer, draft);

describe('validateDraft', () => {
  it('accepts a valid draft', () => {
    expect(validateDraft(draft)).toEqual({});
  });

  it('reports condition problems at the server paths', () => {
    const clause = draft.rules[0]!.clauses[0]!;
    const errors = validateDraft(
      edit(
        {
          type: 'updateClause',
          ruleId: 'r_paid01',
          uid: clause.uid,
          changes: { attribute: '1plan', values: [] },
        },
        {
          type: 'updateClause',
          ruleId: 'r_staff1',
          uid: draft.rules[1]!.clauses[0]!.uid,
          changes: { operator: 'gte', values: ['18', 'abc'] },
        },
      ),
    );

    expect(errors).toEqual({
      'rules[0].clauses[0].attribute': 'Use a valid attribute name, such as email or plan.',
      'rules[0].clauses[0].values': 'Add at least one value.',
      'rules[1].clauses[0].values':
        "'abc' is not a number. Use digits with an optional decimal point, such as 18 or 2.5.",
    });
  });

  it('checks rollout totals and entries', () => {
    const serve = draft.rules[1]!.serve;
    const errors = validateDraft(
      edit({
        type: 'setServe',
        location: { kind: 'rule', ruleId: 'r_staff1' },
        serve: {
          ...serve,
          percents: serve.percents.map((item, index) => ({
            ...item,
            percent: ['50', '40', '0'][index]!,
          })),
        },
      }),
    );
    expect(errors).toEqual({
      'rules[1].serve.rollout.weights': 'Weights add up to 90%. Make them add up to 100%.',
    });

    const invalid = validateDraft(
      edit({
        type: 'setServe',
        location: { kind: 'fallthrough' },
        serve: {
          ...draft.fallthrough,
          mode: 'rollout',
          bucketBy: '9',
          percents: draft.fallthrough.percents.map((item) => ({ ...item, percent: '101' })),
        },
      }),
    );
    expect(invalid['fallthrough.rollout.bucketBy']).toBe(
      'Bucket by a valid attribute name, such as key.',
    );
    expect(errorUnder(invalid, 'fallthrough.rollout.weights[0]')).toBe(
      'Enter a percentage from 0 to 100, with up to three decimals.',
    );
  });

  it('rejects a context key in two target lists', () => {
    const errors = validateDraft(
      edit({ type: 'setTargetKeys', variationId: 'v_third1', contextKeys: ['alice'] }),
    );
    expect(errors).toEqual({
      'targets[2].contextKeys':
        "'alice' is already targeted by another variation. A context key can be in only one list.",
    });
  });

  it('shows only rollout problems before the person asks to review', () => {
    expect(
      liveErrors({
        'rules[0].clauses[0].values': 'Add at least one value.',
        'fallthrough.rollout.weights': 'Weights add up to 90%. Make them add up to 100%.',
      }),
    ).toEqual({
      'fallthrough.rollout.weights': 'Weights add up to 90%. Make them add up to 100%.',
    });
  });
});

describe('fromServerErrors', () => {
  it('removes the prefix and shows weight errors on the whole rollout', () => {
    expect(
      fromServerErrors(
        {
          'config.rules[1].clauses[0].values[2]': ["'x' is not a number."],
          'config.fallthrough.rollout.weights[1].variationId': [
            "Variation 'v_gone' does not exist on this flag.",
          ],
          comment: ['Add a comment.'],
        },
        'config.',
      ),
    ).toEqual({
      'rules[1].clauses[0].values[2]': "'x' is not a number.",
      'fallthrough.rollout.weights': "Variation 'v_gone' does not exist on this flag.",
    });
  });
});
