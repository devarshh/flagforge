import type {
  Clause,
  ClauseOperator,
  Rule,
  Serve,
  TargetingConfig,
  Variation,
  WeightedVariation,
} from '../../api/types';
import { percentToWeight, weightToPercent } from './rollout';

/**
 * The editable form of one environment's targeting. It keeps what the editor needs but the API does not: every
 * variation's target list and rollout percentage (as typed), both serve modes, and row ids for conditions.
 * `toConfig` sends every variation's target list, so server error paths such as `targets[1]` index the same array
 * as the draft (the server drops empty lists). Rollouts list only variations with a non-zero weight, because the
 * server treats every listed variation as in use, which would block removing it.
 */
export interface DraftClause {
  uid: string;
  attribute: string;
  operator: ClauseOperator;
  negate: boolean;
  values: string[];
}

export interface DraftPercent {
  variationId: string;
  percent: string;
}

export interface DraftServe {
  mode: 'variation' | 'rollout';
  variationId: string;
  bucketBy: string;
  percents: DraftPercent[];
}

export interface DraftRule {
  id: string;
  description: string;
  clauses: DraftClause[];
  serve: DraftServe;
}

export interface DraftTarget {
  variationId: string;
  contextKeys: string[];
}

export interface TargetingDraft {
  enabled: boolean;
  offVariationId: string;
  targets: DraftTarget[];
  rules: DraftRule[];
  fallthrough: DraftServe;
}

export type ServeLocation = { kind: 'fallthrough' } | { kind: 'rule'; ruleId: string };

export type DraftAction =
  | { type: 'reset'; draft: TargetingDraft }
  | { type: 'setEnabled'; enabled: boolean }
  | { type: 'setOffVariation'; variationId: string }
  | { type: 'setTargetKeys'; variationId: string; contextKeys: string[] }
  | { type: 'insertRule'; rule: DraftRule; afterRuleId?: string }
  | { type: 'removeRule'; ruleId: string }
  | { type: 'moveRule'; ruleId: string; offset: -1 | 1 }
  | { type: 'setRuleDescription'; ruleId: string; description: string }
  | { type: 'addClause'; ruleId: string; clause: DraftClause }
  | {
      type: 'updateClause';
      ruleId: string;
      uid: string;
      changes: Partial<Omit<DraftClause, 'uid'>>;
    }
  | { type: 'removeClause'; ruleId: string; uid: string }
  | { type: 'setServe'; location: ServeLocation; serve: DraftServe };

const idAlphabet = 'abcdefghijklmnopqrstuvwxyz0123456789';

/** A rule id in the server's style: `r_` and six lowercase letters or digits. */
export function newRuleId(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(6));
  return `r_${Array.from(bytes, (byte) => idAlphabet[byte % idAlphabet.length]).join('')}`;
}

let clauseCounter = 0;

export function newClauseUid(): string {
  clauseCounter += 1;
  return `clause-${clauseCounter}`;
}

export function createServe(serve: Serve, variations: readonly Variation[]): DraftServe {
  const rollout = serve.rollout ?? null;
  const variationId = serve.variationId ?? variations[0]?.id ?? '';
  const weightOf = (id: string) =>
    rollout?.weights.find((weight) => weight.variationId === id)?.weight ?? 0;
  return {
    mode: rollout ? 'rollout' : 'variation',
    variationId,
    bucketBy: rollout?.bucketBy ?? 'key',
    // Switching a fixed serve to a rollout starts from "100% of the current variation".
    percents: variations.map((variation) => ({
      variationId: variation.id,
      percent: rollout
        ? weightToPercent(weightOf(variation.id))
        : variation.id === variationId
          ? '100'
          : '0',
    })),
  };
}

export function createClause(clause?: Clause): DraftClause {
  return {
    uid: newClauseUid(),
    attribute: clause?.attribute ?? '',
    operator: clause?.operator ?? 'in',
    negate: clause?.negate ?? false,
    values: [...(clause?.values ?? [])],
  };
}

export function createRule(rule: Rule, variations: readonly Variation[]): DraftRule {
  return {
    id: rule.id,
    description: rule.description ?? '',
    clauses: rule.clauses.map(createClause),
    serve: createServe(rule.serve, variations),
  };
}

export function createDraft(
  config: TargetingConfig,
  variations: readonly Variation[],
): TargetingDraft {
  return {
    enabled: config.enabled,
    offVariationId: config.offVariationId,
    targets: variations.map((variation) => ({
      variationId: variation.id,
      contextKeys: [
        ...(config.targets.find((target) => target.variationId === variation.id)?.contextKeys ??
          []),
      ],
    })),
    rules: config.rules.map((rule) => createRule(rule, variations)),
    fallthrough: createServe(config.fallthrough, variations),
  };
}

export function toServe(serve: DraftServe): Serve {
  if (serve.mode === 'variation') {
    return { variationId: serve.variationId };
  }

  const weights: WeightedVariation[] = serve.percents
    .map((item) => ({
      variationId: item.variationId,
      // Invalid entries never reach the server: the editor blocks review until they are fixed.
      weight: percentToWeight(item.percent) ?? Number.NaN,
    }))
    .filter((item) => item.weight !== 0);
  return { rollout: { bucketBy: serve.bucketBy.trim() || 'key', weights } };
}

export function toConfig(draft: TargetingDraft): TargetingConfig {
  return {
    enabled: draft.enabled,
    offVariationId: draft.offVariationId,
    targets: draft.targets.map((target) => ({
      variationId: target.variationId,
      contextKeys: target.contextKeys,
    })),
    rules: draft.rules.map((rule) => ({
      id: rule.id,
      description: rule.description.trim() || null,
      clauses: rule.clauses.map((clause) => ({
        attribute: clause.attribute.trim(),
        operator: clause.operator,
        values: clause.operator === 'exists' ? [] : clause.values,
        negate: clause.negate,
      })),
      serve: toServe(rule.serve),
    })),
    fallthrough: toServe(draft.fallthrough),
  };
}

/** A new rule with one empty condition, serving the first variation. */
export function newRuleAction(variations: readonly Variation[]): DraftAction {
  return {
    type: 'insertRule',
    rule: {
      id: newRuleId(),
      description: '',
      clauses: [createClause()],
      serve: createServe({ variationId: variations[0]?.id ?? '' }, variations),
    },
  };
}

/** A copy of a rule (new rule id and condition ids), inserted right after it. */
export function duplicateRuleAction(rule: DraftRule): DraftAction {
  return {
    type: 'insertRule',
    afterRuleId: rule.id,
    rule: {
      ...rule,
      id: newRuleId(),
      clauses: rule.clauses.map((clause) => ({
        ...clause,
        uid: newClauseUid(),
        values: [...clause.values],
      })),
      serve: { ...rule.serve, percents: rule.serve.percents.map((item) => ({ ...item })) },
    },
  };
}

function mapRule(
  draft: TargetingDraft,
  ruleId: string,
  change: (rule: DraftRule) => DraftRule,
): TargetingDraft {
  return { ...draft, rules: draft.rules.map((rule) => (rule.id === ruleId ? change(rule) : rule)) };
}

export function targetingReducer(draft: TargetingDraft, action: DraftAction): TargetingDraft {
  switch (action.type) {
    case 'reset':
      return action.draft;
    case 'setEnabled':
      return { ...draft, enabled: action.enabled };
    case 'setOffVariation':
      return { ...draft, offVariationId: action.variationId };
    case 'setTargetKeys':
      return {
        ...draft,
        targets: draft.targets.map((target) =>
          target.variationId === action.variationId
            ? { ...target, contextKeys: [...new Set(action.contextKeys)] }
            : target,
        ),
      };
    case 'insertRule': {
      const index =
        action.afterRuleId === undefined
          ? draft.rules.length
          : draft.rules.findIndex((rule) => rule.id === action.afterRuleId) + 1;
      const position = index <= 0 && action.afterRuleId !== undefined ? draft.rules.length : index;
      return {
        ...draft,
        rules: [...draft.rules.slice(0, position), action.rule, ...draft.rules.slice(position)],
      };
    }
    case 'removeRule':
      return { ...draft, rules: draft.rules.filter((rule) => rule.id !== action.ruleId) };
    case 'moveRule': {
      const index = draft.rules.findIndex((rule) => rule.id === action.ruleId);
      const target = index + action.offset;
      if (index < 0 || target < 0 || target >= draft.rules.length) {
        return draft;
      }

      const rules = [...draft.rules];
      [rules[index], rules[target]] = [rules[target]!, rules[index]!];
      return { ...draft, rules };
    }
    case 'setRuleDescription':
      return mapRule(draft, action.ruleId, (rule) => ({
        ...rule,
        description: action.description,
      }));
    case 'addClause':
      return mapRule(draft, action.ruleId, (rule) => ({
        ...rule,
        clauses: [...rule.clauses, action.clause],
      }));
    case 'updateClause':
      return mapRule(draft, action.ruleId, (rule) => ({
        ...rule,
        clauses: rule.clauses.map((clause) => {
          if (clause.uid !== action.uid) {
            return clause;
          }

          const next = { ...clause, ...action.changes };
          // "exists" conditions take no values.
          return next.operator === 'exists' ? { ...next, values: [] } : next;
        }),
      }));
    case 'removeClause':
      return mapRule(draft, action.ruleId, (rule) => ({
        ...rule,
        clauses: rule.clauses.filter((clause) => clause.uid !== action.uid),
      }));
    case 'setServe':
      return action.location.kind === 'fallthrough'
        ? { ...draft, fallthrough: action.serve }
        : mapRule(draft, action.location.ruleId, (rule) => ({ ...rule, serve: action.serve }));
  }
}

/** Whether two configs are the same, as the API would receive them. */
export function sameConfig(left: TargetingConfig, right: TargetingConfig): boolean {
  return JSON.stringify(left) === JSON.stringify(right);
}
