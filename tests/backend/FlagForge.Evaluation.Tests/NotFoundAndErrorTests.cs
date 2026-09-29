using static FlagForge.Evaluation.Tests.Build;

namespace FlagForge.Evaluation.Tests;

public sealed class NotFoundAndErrorTests
{
    [Fact]
    public void Missing_flag_returns_flag_not_found_without_a_value()
    {
        var result = FlagEvaluator.Evaluate("does-not-exist", null, Context("alice"));

        result.ShouldBe(new EvaluationResult("does-not-exist", null, null, EvaluationReason.FlagNotFound));
    }

    [Fact]
    public void Archived_flag_returns_flag_not_found()
    {
        var flag = BooleanFlag(Config(targets: [Target("true", "alice")]), isArchived: true);

        var result = FlagEvaluator.Evaluate(FlagKey, flag, Context("alice"));

        result.Reason.Kind.ShouldBe(EvaluationReasonKind.FlagNotFound);
        result.Value.ShouldBeNull();
        result.VariationId.ShouldBeNull();
    }

    public static TheoryData<string, TargetingConfig> BrokenConfigs => new()
    {
        { "off variation", Config(enabled: false, offVariationId: "gone") },
        { "target variation", Config(targets: [Target("gone", "alice")]) },
        { "rule variation", Config(rules: [Rule("r1", Serve.Variation("gone"), Clause("key", ClauseOperator.Exists))]) },
        { "fallthrough variation", Config(fallthrough: Serve.Variation("gone")) },
        { "rollout variation", Config(fallthrough: Rollout(("gone", 100_000))) },
        { "serve with neither variation nor rollout", Config(fallthrough: new Serve()) },
        { "serve with both variation and rollout", Config(fallthrough: Rollout(("true", 100_000)) with { VariationId = "true" }) },
        { "rollout without weights", Config(fallthrough: Rollout()) },
    };

    [Theory]
    [MemberData(nameof(BrokenConfigs))]
    public void Broken_variation_reference_returns_error_without_a_value(string scenario, TargetingConfig config)
    {
        var result = Evaluate(config, Context("alice"));

        result.Reason.ShouldBe(EvaluationReason.Error, scenario);
        result.Value.ShouldBeNull();
        result.VariationId.ShouldBeNull();
        result.FlagKey.ShouldBe(FlagKey);
    }

    [Fact]
    public void Evaluate_guards_its_arguments()
    {
        Should.Throw<ArgumentNullException>(() => FlagEvaluator.Evaluate(null!, null, Context("a")));
        Should.Throw<ArgumentNullException>(() => FlagEvaluator.Evaluate("k", null, null!));
        Should.Throw<ArgumentException>(() => CompiledFlag.Compile("", Salt, BooleanVariations, Config()));
    }
}
