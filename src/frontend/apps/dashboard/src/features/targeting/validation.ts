import { isNumericOperator } from './operators';
import { percentFormatMessage, percentToWeight, rolloutTotalError } from './rollout';
import type { DraftServe, TargetingDraft } from './draft';

/** Messages keyed by field path, in the server's format relative to the config (`rules[0].clauses[1].values`). */
export type DraftErrors = Readonly<Record<string, string>>;

const attributePattern = /^[A-Za-z_][A-Za-z0-9_.-]{0,63}$/;
// The server parses clause numbers with invariant-culture decimal rules: sign, decimal point, and exponent allowed.
const numberPattern = /^\s*[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?\s*$/;

export function isValidAttribute(name: string): boolean {
  return attributePattern.test(name);
}

/** The server's targeting rules (TargetingValidator), checked while editing. */
export function validateDraft(draft: TargetingDraft): DraftErrors {
  const errors: Record<string, string> = {};
  const add = (path: string, message: string) => {
    errors[path] ??= message;
  };

  if (!draft.offVariationId) {
    add('offVariationId', 'Choose a variation.');
  }

  const targetedBy = new Map<string, string>();
  draft.targets.forEach((target, index) => {
    const path = `targets[${index}].contextKeys`;
    if (target.contextKeys.length > 1000) {
      add(path, 'A variation can target at most 1000 context keys.');
      return;
    }

    for (const key of target.contextKeys) {
      const owner = targetedBy.get(key);
      if (key.length === 0 || key.length > 256) {
        add(path, 'Context keys must be between 1 and 256 characters.');
      } else if (owner !== undefined && owner !== target.variationId) {
        add(
          path,
          `'${key}' is already targeted by another variation. A context key can be in only one list.`,
        );
      } else {
        targetedBy.set(key, target.variationId);
      }
    }
  });

  if (draft.rules.length > 50) {
    add('rules', 'A flag can have at most 50 rules in an environment.');
  }

  draft.rules.forEach((rule, ruleIndex) => {
    const path = `rules[${ruleIndex}]`;
    if (rule.description.trim().length > 200) {
      add(`${path}.description`, 'Keep the description to 200 characters or fewer.');
    }

    if (rule.clauses.length < 1 || rule.clauses.length > 10) {
      add(`${path}.clauses`, 'A rule needs between 1 and 10 conditions.');
    }

    rule.clauses.forEach((clause, clauseIndex) => {
      const clausePath = `${path}.clauses[${clauseIndex}]`;
      if (!isValidAttribute(clause.attribute.trim())) {
        add(`${clausePath}.attribute`, 'Use a valid attribute name, such as email or plan.');
      }

      if (clause.operator === 'exists') {
        return;
      }

      if (clause.values.length === 0) {
        add(`${clausePath}.values`, 'Add at least one value.');
      } else if (clause.values.length > 500) {
        add(`${clausePath}.values`, 'A condition can have at most 500 values.');
      } else if (isNumericOperator(clause.operator)) {
        const invalid = clause.values.find((value) => !numberPattern.test(value));
        if (invalid !== undefined) {
          add(
            `${clausePath}.values`,
            `'${invalid}' is not a number. Use digits with an optional decimal point, such as 18 or 2.5.`,
          );
        }
      }
    });

    validateServe(rule.serve, `${path}.serve`, add);
  });

  validateServe(draft.fallthrough, 'fallthrough', add);
  return errors;
}

/** The serve rules on their own, for editors outside the targeting tab (scheduled default-rule changes). */
export function validateServeDraft(serve: DraftServe, path: string): DraftErrors {
  const errors: Record<string, string> = {};
  validateServe(serve, path, (key, message) => {
    errors[key] ??= message;
  });
  return errors;
}

function validateServe(
  serve: DraftServe,
  path: string,
  add: (path: string, message: string) => void,
) {
  if (serve.mode === 'variation') {
    if (!serve.variationId) {
      add(`${path}.variationId`, 'Choose a variation.');
    }

    return;
  }

  const rolloutPath = `${path}.rollout`;
  if (!isValidAttribute(serve.bucketBy.trim() || 'key')) {
    add(`${rolloutPath}.bucketBy`, 'Bucket by a valid attribute name, such as key.');
  }

  serve.percents.forEach((item, index) => {
    if (percentToWeight(item.percent) === null) {
      add(`${rolloutPath}.weights[${index}].weight`, percentFormatMessage);
    }
  });

  const totalError = rolloutTotalError(serve.percents.map((item) => item.percent));
  if (totalError) {
    add(`${rolloutPath}.weights`, totalError);
  }
}

/** Rollout problems are shown while typing; other problems appear once the person asks to review. */
export function liveErrors(errors: DraftErrors): DraftErrors {
  return Object.fromEntries(Object.entries(errors).filter(([path]) => path.includes('.rollout')));
}

/** The message for exactly this path. */
export function errorAt(errors: DraftErrors, path: string): string | undefined {
  return errors[path];
}

/** The first message for this path or any path below it (`path[0]`, `path.name`). */
export function errorUnder(errors: DraftErrors, path: string): string | undefined {
  for (const [key, message] of Object.entries(errors)) {
    if (key === path || key.startsWith(`${path}[`) || key.startsWith(`${path}.`)) {
      return message;
    }
  }

  return undefined;
}

/**
 * Converts server errors (`config.rules[0].id` for a save) into draft errors by removing the prefix. Errors on one
 * rollout weight are shown on the rollout as a whole, because the request lists only non-zero weights, so its
 * indexes do not match the editor's rows.
 */
export function fromServerErrors(
  errors: Readonly<Record<string, readonly string[]>>,
  prefix: string,
): DraftErrors {
  return Object.fromEntries(
    Object.entries(errors)
      .filter(([path, messages]) => path.startsWith(prefix) && messages.length > 0)
      .map(([path, messages]) => [
        path.slice(prefix.length).replace(/(\.rollout\.weights)\[\d+\].*$/, '$1'),
        messages[0]!,
      ]),
  );
}
