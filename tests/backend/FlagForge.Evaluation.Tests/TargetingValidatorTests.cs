using static FlagForge.Evaluation.Tests.Build;

namespace FlagForge.Evaluation.Tests;

public sealed class TargetingValidatorTests
{
    private static readonly Clause ValidClause = Clause("plan", ClauseOperator.In, "premium");

    [Fact]
    public void Valid_config_has_no_errors()
    {
        var config = Config(
            targets: [Target("true", "alice", "alice"), Target("false", "bob")],
            rules:
            [
                Rule("r1", Serve.Variation("true"), Clause("email", ClauseOperator.EndsWith, "@acme.com"), Not(Clause("plan", ClauseOperator.In, "free"))),
                Rule("r2", RolloutBy("company", ("true", 33_333), ("false", 66_667)), Clause("age", ClauseOperator.Gte, "18", "2.5", "-4", "1e3")),
                Rule("r3", Serve.Variation("false"), Clause("beta", ClauseOperator.Exists)),
            ],
            fallthrough: Rollout(("true", 0), ("false", 100_000)));

        Validate(config).ShouldBeEmpty();
    }

    [Fact]
    public void Off_variation_must_exist()
    {
        ShouldHaveError(Validate(Config(offVariationId: "gone")), "offVariationId", "Variation 'gone' does not exist on this flag.");
        ShouldHaveError(Validate(Config(offVariationId: "")), "offVariationId", "Choose a variation.");
    }

    [Fact]
    public void Target_variation_must_exist()
    {
        ShouldHaveError(Validate(Config(targets: [Target("gone", "alice")])), "targets[0].variationId");
    }

    [Fact]
    public void Context_key_may_appear_in_only_one_target_list()
    {
        var errors = Validate(Config(targets: [Target("true", "alice", "bob"), Target("false", "carol", "alice")]));

        ShouldHaveError(errors, "targets[1].contextKeys[1]", "'alice' is already targeted by another variation");
        errors.Count.ShouldBe(1);
    }

    [Fact]
    public void Target_list_is_limited_to_one_thousand_keys()
    {
        var keys = Enumerable.Range(0, 1001).Select(i => $"user-{i}").ToArray();

        ShouldHaveError(Validate(Config(targets: [Target("true", keys)])), "targets[0].contextKeys", "at most 1000");
        Validate(Config(targets: [Target("true", keys[..1000])])).ShouldBeEmpty();
    }

    [Fact]
    public void Target_context_keys_must_be_one_to_256_characters()
    {
        var errors = Validate(Config(targets: [Target("true", "ok", "", new string('k', 257))]));

        ShouldHaveError(errors, "targets[0].contextKeys[1]");
        ShouldHaveError(errors, "targets[0].contextKeys[2]");
    }

    [Fact]
    public void Missing_list_entries_are_reported()
    {
        var config = Config(targets: [null!], rules: [null!, Rule("r1", Serve.Variation("true"), [null!])]);

        var errors = Validate(config);

        ShouldHaveError(errors, "targets[0]", "Target is missing.");
        ShouldHaveError(errors, "rules[0]", "Rule is missing.");
        ShouldHaveError(errors, "rules[1].clauses[0]", "Condition is missing.");
    }

    [Fact]
    public void At_most_fifty_rules()
    {
        var rules = Enumerable.Range(0, 51).Select(i => Rule($"r{i}", Serve.Variation("true"), ValidClause)).ToArray();

        ShouldHaveError(Validate(Config(rules: rules)), "rules", "at most 50 rules");
        Validate(Config(rules: rules[..50])).ShouldBeEmpty();
    }

    [Fact]
    public void Rule_ids_are_required_and_unique()
    {
        var errors = Validate(Config(rules:
        [
            Rule("r1", Serve.Variation("true"), ValidClause),
            Rule("r1", Serve.Variation("true"), ValidClause),
            Rule("", Serve.Variation("true"), ValidClause),
            Rule(new string('r', 65), Serve.Variation("true"), ValidClause),
        ]));

        ShouldHaveError(errors, "rules[1].id", "Another rule already uses the id 'r1'");
        ShouldHaveError(errors, "rules[2].id");
        ShouldHaveError(errors, "rules[3].id");
    }

    [Fact]
    public void Rule_description_is_limited_to_200_characters()
    {
        var rule = Rule("r1", Serve.Variation("true"), ValidClause) with { Description = new string('d', 201) };

        ShouldHaveError(Validate(Config(rules: [rule])), "rules[0].description");
        Validate(Config(rules: [rule with { Description = new string('d', 200) }])).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Rule_needs_one_to_ten_clauses(int count)
    {
        var clauses = Enumerable.Repeat(ValidClause, count).ToArray();

        ShouldHaveError(Validate(Config(rules: [Rule("r1", Serve.Variation("true"), clauses)])), "rules[0].clauses", "between 1 and 10 conditions");
    }

    [Theory]
    [InlineData("")]
    [InlineData("1st")]
    [InlineData("has space")]
    [InlineData("toolong_toolong_toolong_toolong_toolong_toolong_toolong_toolong_x")]
    public void Clause_attribute_must_be_a_valid_attribute_name(string attribute)
    {
        var rule = Rule("r1", Serve.Variation("true"), Clause(attribute, ClauseOperator.In, "x"));

        ShouldHaveError(Validate(Config(rules: [rule])), "rules[0].clauses[0].attribute");
    }

    [Fact]
    public void Clause_operator_must_be_known()
    {
        var rule = Rule("r1", Serve.Variation("true"), Clause("plan", (ClauseOperator)99, "x"));

        ShouldHaveError(Validate(Config(rules: [rule])), "rules[0].clauses[0].operator", "Choose an operator.");
    }

    [Fact]
    public void Exists_clause_takes_no_values()
    {
        var rule = Rule("r1", Serve.Variation("true"), Clause("plan", ClauseOperator.Exists, "premium"));

        ShouldHaveError(Validate(Config(rules: [rule])), "rules[0].clauses[0].values", "'exists' conditions do not use values");
    }

    [Theory]
    [InlineData(ClauseOperator.In)]
    [InlineData(ClauseOperator.Contains)]
    [InlineData(ClauseOperator.Lt)]
    public void Other_operators_need_at_least_one_value(ClauseOperator op)
    {
        var rule = Rule("r1", Serve.Variation("true"), Clause("plan", op));

        ShouldHaveError(Validate(Config(rules: [rule])), "rules[0].clauses[0].values", "Add at least one value.");
    }

    [Fact]
    public void Clause_is_limited_to_500_values()
    {
        var values = Enumerable.Range(0, 501).Select(i => $"v{i}").ToArray();

        ShouldHaveError(Validate(Config(rules: [Rule("r1", Serve.Variation("true"), Clause("plan", ClauseOperator.In, values))])), "rules[0].clauses[0].values", "at most 500 values");
        Validate(Config(rules: [Rule("r1", Serve.Variation("true"), Clause("plan", ClauseOperator.In, values[..500]))])).ShouldBeEmpty();
    }

    [Fact]
    public void Clause_values_cannot_be_null()
    {
        var rule = Rule("r1", Serve.Variation("true"), Clause("plan", ClauseOperator.In, "a", null!));

        ShouldHaveError(Validate(Config(rules: [rule])), "rules[0].clauses[0].values[1]", "Value is missing.");
    }

    [Theory]
    [InlineData(ClauseOperator.Lt)]
    [InlineData(ClauseOperator.Lte)]
    [InlineData(ClauseOperator.Gt)]
    [InlineData(ClauseOperator.Gte)]
    public void Numeric_operator_values_must_parse_as_decimals(ClauseOperator op)
    {
        var rule = Rule("r1", Serve.Variation("true"), Clause("age", op, "18", "eighteen", "1,000"));

        var errors = Validate(Config(rules: [rule]));

        ShouldHaveError(errors, "rules[0].clauses[0].values[1]", "'eighteen' is not a number");
        ShouldHaveError(errors, "rules[0].clauses[0].values[2]");
        errors.Count.ShouldBe(2);
    }

    [Fact]
    public void In_operator_accepts_non_numeric_values()
    {
        Validate(Config(rules: [Rule("r1", Serve.Variation("true"), Clause("age", ClauseOperator.In, "eighteen", "18"))])).ShouldBeEmpty();
    }

    [Fact]
    public void Serve_needs_exactly_one_of_variation_or_rollout()
    {
        var neither = Rule("r1", new Serve(), ValidClause);
        var both = Rule("r2", Rollout(("true", 100_000)) with { VariationId = "true" }, ValidClause);

        var errors = Validate(Config(rules: [neither, both], fallthrough: null!));

        ShouldHaveError(errors, "rules[0].serve", "Serve either a single variation or a percentage rollout.");
        ShouldHaveError(errors, "rules[1].serve");
    }

    [Fact]
    public void Missing_fallthrough_is_reported()
    {
        var config = Config() with { Fallthrough = null! };

        ShouldHaveError(Validate(config), "fallthrough", "Choose what to serve.");
    }

    [Fact]
    public void Serve_variation_must_exist()
    {
        ShouldHaveError(Validate(Config(rules: [Rule("r1", Serve.Variation("gone"), ValidClause)])), "rules[0].serve.variationId");
        ShouldHaveError(Validate(Config(fallthrough: Serve.Variation("gone"))), "fallthrough.variationId");
    }

    [Fact]
    public void Rollout_weights_must_add_up_to_100_percent()
    {
        var errors = Validate(Config(fallthrough: Rollout(("true", 40_000), ("false", 50_000))));

        ShouldHaveError(errors, "fallthrough.rollout.weights", "Weights add up to 90%. Make them add up to 100%.");
    }

    [Fact]
    public void Rollout_sum_message_shows_fractional_percentages()
    {
        var errors = Validate(Config(fallthrough: Rollout(("true", 33_333), ("false", 33_333))));

        ShouldHaveError(errors, "fallthrough.rollout.weights", "Weights add up to 66.666%.");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100_001)]
    public void Rollout_weights_must_be_between_0_and_100000(int weight)
    {
        var errors = Validate(Config(fallthrough: Rollout(("true", weight), ("false", 100_000 - weight))));

        ShouldHaveError(errors, "fallthrough.rollout.weights[0].weight", "Use a percentage between 0% and 100%.");
        errors.ShouldNotContain(e => e.Path == "fallthrough.rollout.weights");
    }

    [Fact]
    public void Rollout_references_each_variation_at_most_once()
    {
        var errors = Validate(Config(rules: [Rule("r1", Rollout(("true", 50_000), ("true", 50_000)), ValidClause)]));

        ShouldHaveError(errors, "rules[0].serve.rollout.weights[1].variationId", "appears more than once");
    }

    [Fact]
    public void Rollout_variations_must_exist()
    {
        ShouldHaveError(Validate(Config(fallthrough: Rollout(("gone", 100_000)))), "fallthrough.rollout.weights[0].variationId");
    }

    [Fact]
    public void Rollout_needs_weights()
    {
        ShouldHaveError(Validate(Config(fallthrough: Rollout())), "fallthrough.rollout.weights", "Add at least one variation");
    }

    [Fact]
    public void Rollout_weight_entries_cannot_be_null()
    {
        var serve = Serve.PercentageRollout(new Rollout { Weights = [null!] });

        ShouldHaveError(Validate(Config(fallthrough: serve)), "fallthrough.rollout.weights[0]", "Weight is missing.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not valid")]
    [InlineData("9lives")]
    public void Bucket_by_must_be_a_valid_attribute_name(string bucketBy)
    {
        var errors = Validate(Config(fallthrough: RolloutBy(bucketBy, ("true", 100_000))));

        ShouldHaveError(errors, "fallthrough.rollout.bucketBy");
    }

    [Fact]
    public void Validate_guards_its_arguments()
    {
        Should.Throw<ArgumentNullException>(() => TargetingValidator.Validate(null!, BooleanVariationIds));
        Should.Throw<ArgumentNullException>(() => TargetingValidator.Validate(Config(), null!));
    }

    private static IReadOnlyList<ValidationError> Validate(TargetingConfig config) =>
        TargetingValidator.Validate(config, BooleanVariationIds);

    private static void ShouldHaveError(IReadOnlyList<ValidationError> errors, string path, string? messageFragment = null)
    {
        errors.ShouldContain(
            e => e.Path == path && (messageFragment == null || e.Message.Contains(messageFragment, StringComparison.Ordinal)),
            $"expected an error at '{path}'; got: {string.Join(" | ", errors.Select(e => $"{e.Path}: {e.Message}"))}");
    }
}
