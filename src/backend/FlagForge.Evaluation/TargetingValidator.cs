using System.Globalization;

namespace FlagForge.Evaluation;

/// <summary>
/// Validates a targeting config against a flag's variations. Pure and shared: the management API runs it before
/// saving and when previewing drafts. Errors carry paths such as <c>rules[1].clauses[0].values</c>.
/// </summary>
public static class TargetingValidator
{
    public static IReadOnlyList<ValidationError> Validate(TargetingConfig config, IReadOnlyList<string> variationIds)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(variationIds);

        var validation = new Validation(variationIds);
        validation.VariationReference(config.OffVariationId, "offVariationId");
        validation.Targets(config.Targets);
        validation.Rules(config.Rules);
        validation.Serve(config.Fallthrough, "fallthrough");
        return validation.Errors;
    }

    private static bool IsNumeric(ClauseOperator op) =>
        op is ClauseOperator.Lt or ClauseOperator.Lte or ClauseOperator.Gt or ClauseOperator.Gte;

    private sealed class Validation(IReadOnlyList<string> variationIds)
    {
        private readonly HashSet<string> _variationIds = new(variationIds, StringComparer.Ordinal);

        public List<ValidationError> Errors { get; } = [];

        public void VariationReference(string? variationId, string path)
        {
            if (string.IsNullOrEmpty(variationId))
            {
                Add(path, "Choose a variation.");
            }
            else if (!_variationIds.Contains(variationId))
            {
                Add(path, $"Variation '{variationId}' does not exist on this flag.");
            }
        }

        public void Targets(IReadOnlyList<Target> targets)
        {
            var targetedKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < targets.Count; i++)
            {
                var path = ValidationPath.Index("targets", i);
                var target = targets[i];
                if (target is null)
                {
                    Add(path, "Target is missing.");
                    continue;
                }

                VariationReference(target.VariationId, ValidationPath.Combine(path, "variationId"));
                var keysPath = ValidationPath.Combine(path, "contextKeys");
                if (target.ContextKeys.Count > TargetingLimits.MaxContextKeysPerTarget)
                {
                    Add(keysPath, "A variation can target at most 1000 context keys.");
                    continue;
                }

                // Duplicates inside one list are harmless (the normalizer removes them); across lists they conflict.
                var keysInThisList = new HashSet<string>(StringComparer.Ordinal);
                for (var k = 0; k < target.ContextKeys.Count; k++)
                {
                    var key = target.ContextKeys[k];
                    var keyPath = ValidationPath.Index(keysPath, k);
                    if (string.IsNullOrEmpty(key) || key.Length > ContextLimits.MaxKeyLength)
                    {
                        Add(keyPath, "Context keys must be between 1 and 256 characters.");
                    }
                    else if (keysInThisList.Add(key) && !targetedKeys.Add(key))
                    {
                        Add(keyPath, $"'{key}' is already targeted by another variation. A context key can be in only one list.");
                    }
                }
            }
        }

        public void Rules(IReadOnlyList<Rule> rules)
        {
            if (rules.Count > TargetingLimits.MaxRules)
            {
                Add("rules", "A flag can have at most 50 rules in an environment.");
            }

            var ruleIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < rules.Count; i++)
            {
                var path = ValidationPath.Index("rules", i);
                var rule = rules[i];
                if (rule is null)
                {
                    Add(path, "Rule is missing.");
                    continue;
                }

                var idPath = ValidationPath.Combine(path, "id");
                if (string.IsNullOrEmpty(rule.Id) || rule.Id.Length > TargetingLimits.MaxRuleIdLength)
                {
                    Add(idPath, "Give the rule an id of at most 64 characters.");
                }
                else if (!ruleIds.Add(rule.Id))
                {
                    Add(idPath, $"Another rule already uses the id '{rule.Id}'. Rule ids must be unique.");
                }

                if (rule.Description is { Length: > TargetingLimits.MaxRuleDescriptionLength })
                {
                    Add(ValidationPath.Combine(path, "description"), "Keep the description to 200 characters or fewer.");
                }

                Clauses(rule.Clauses, ValidationPath.Combine(path, "clauses"));
                Serve(rule.Serve, ValidationPath.Combine(path, "serve"));
            }
        }

        public void Serve(Serve? serve, string path)
        {
            if (serve is null)
            {
                Add(path, "Choose what to serve.");
                return;
            }

            if ((serve.VariationId is null) == (serve.Rollout is null))
            {
                Add(path, "Serve either a single variation or a percentage rollout.");
                return;
            }

            if (serve.Rollout is null)
            {
                VariationReference(serve.VariationId, ValidationPath.Combine(path, "variationId"));
                return;
            }

            Rollout(serve.Rollout, ValidationPath.Combine(path, "rollout"));
        }

        private void Rollout(Rollout rollout, string path)
        {
            if (!AttributeNames.IsValid(rollout.BucketBy))
            {
                Add(ValidationPath.Combine(path, "bucketBy"), "Bucket by a valid attribute name, such as key.");
            }

            var weightsPath = ValidationPath.Combine(path, "weights");
            if (rollout.Weights.Count == 0)
            {
                Add(weightsPath, "Add at least one variation to the rollout.");
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            long total = 0;
            var weightsValid = true;
            for (var i = 0; i < rollout.Weights.Count; i++)
            {
                var weightPath = ValidationPath.Index(weightsPath, i);
                var weight = rollout.Weights[i];
                if (weight is null)
                {
                    Add(weightPath, "Weight is missing.");
                    weightsValid = false;
                    continue;
                }

                var variationPath = ValidationPath.Combine(weightPath, "variationId");
                VariationReference(weight.VariationId, variationPath);
                if (weight.VariationId is not null && !seen.Add(weight.VariationId))
                {
                    Add(variationPath, $"Variation '{weight.VariationId}' appears more than once in this rollout.");
                }

                if (weight.Weight is < 0 or > TargetingLimits.TotalWeight)
                {
                    Add(ValidationPath.Combine(weightPath, "weight"), "Use a percentage between 0% and 100%.");
                    weightsValid = false;
                    continue;
                }

                total += weight.Weight;
            }

            if (weightsValid && total != TargetingLimits.TotalWeight)
            {
                var percent = (total / 1000m).ToString("0.###", CultureInfo.InvariantCulture);
                Add(weightsPath, $"Weights add up to {percent}%. Make them add up to 100%.");
            }
        }

        private void Clauses(IReadOnlyList<Clause> clauses, string path)
        {
            if (clauses.Count is < TargetingLimits.MinClausesPerRule or > TargetingLimits.MaxClausesPerRule)
            {
                Add(path, "A rule needs between 1 and 10 conditions.");
            }

            for (var j = 0; j < clauses.Count; j++)
            {
                var clausePath = ValidationPath.Index(path, j);
                var clause = clauses[j];
                if (clause is null)
                {
                    Add(clausePath, "Condition is missing.");
                    continue;
                }

                if (!AttributeNames.IsValid(clause.Attribute))
                {
                    Add(ValidationPath.Combine(clausePath, "attribute"), "Use a valid attribute name, such as email or plan.");
                }

                if (!Enum.IsDefined(clause.Operator))
                {
                    Add(ValidationPath.Combine(clausePath, "operator"), "Choose an operator.");
                    continue;
                }

                ClauseValues(clause, ValidationPath.Combine(clausePath, "values"));
            }
        }

        private void ClauseValues(Clause clause, string path)
        {
            var values = clause.Values;
            if (clause.Operator == ClauseOperator.Exists)
            {
                if (values.Count > 0)
                {
                    Add(path, "Remove the values: 'exists' conditions do not use values.");
                }

                return;
            }

            if (values.Count == 0)
            {
                Add(path, "Add at least one value.");
                return;
            }

            if (values.Count > TargetingLimits.MaxValuesPerClause)
            {
                Add(path, "A condition can have at most 500 values.");
                return;
            }

            for (var k = 0; k < values.Count; k++)
            {
                var value = values[k];
                if (value is null)
                {
                    Add(ValidationPath.Index(path, k), "Value is missing.");
                }
                else if (IsNumeric(clause.Operator) && !NumberParsing.TryParse(value, out _))
                {
                    Add(ValidationPath.Index(path, k), $"'{value}' is not a number. Use digits with an optional decimal point, such as 18 or 2.5.");
                }
            }
        }

        private void Add(string path, string message) => Errors.Add(new ValidationError(path, message));
    }
}
