using static FlagForge.Evaluation.Tests.Build;

namespace FlagForge.Evaluation.Tests;

public sealed class TargetingNormalizerTests
{
    private static readonly string[] VariationOrder = ["v_a", "v_b", "v_c"];

    [Fact]
    public void Rollout_weights_are_put_in_variation_order()
    {
        var config = Config(
            offVariationId: "v_a",
            fallthrough: Rollout(("v_c", 10_000), ("v_a", 60_000), ("v_b", 30_000)),
            rules: [Rule("r1", Rollout(("v_b", 50_000), ("v_a", 50_000)), Clause("key", ClauseOperator.Exists))]);

        var normalized = TargetingNormalizer.Normalize(config, VariationOrder);

        normalized.Fallthrough.Rollout!.Weights.Select(w => w.VariationId).ShouldBe(["v_a", "v_b", "v_c"]);
        normalized.Fallthrough.Rollout.Weights.Select(w => w.Weight).ShouldBe([60_000, 30_000, 10_000]);
        normalized.Rules[0].Serve.Rollout!.Weights.Select(w => w.VariationId).ShouldBe(["v_a", "v_b"]);
    }

    [Fact]
    public void Unknown_variations_sort_last()
    {
        var config = Config(offVariationId: "v_a", fallthrough: Rollout(("zzz", 1), ("v_b", 99_999)));

        var normalized = TargetingNormalizer.Normalize(config, VariationOrder);

        normalized.Fallthrough.Rollout!.Weights.Select(w => w.VariationId).ShouldBe(["v_b", "zzz"]);
    }

    [Fact]
    public void Duplicate_context_keys_are_removed_and_empty_lists_dropped()
    {
        var config = Config(offVariationId: "v_a", targets: [Target("v_a", "alice", "bob", "alice"), Target("v_b")]);

        var normalized = TargetingNormalizer.Normalize(config, VariationOrder);

        normalized.Targets.Count.ShouldBe(1);
        normalized.Targets[0].ContextKeys.ShouldBe(["alice", "bob"]);
    }

    [Fact]
    public void Fixed_serves_and_other_fields_are_unchanged()
    {
        var rule = Rule("r1", Serve.Variation("v_c"), Clause("plan", ClauseOperator.In, "premium")) with { Description = "Paid" };
        var config = Config(enabled: false, offVariationId: "v_b", fallthrough: Serve.Variation("v_a"), rules: [rule]);

        var normalized = TargetingNormalizer.Normalize(config, VariationOrder);

        normalized.Enabled.ShouldBeFalse();
        normalized.OffVariationId.ShouldBe("v_b");
        normalized.Fallthrough.ShouldBe(config.Fallthrough);
        normalized.Rules[0].ShouldBe(rule);
    }

    [Fact]
    public void Normalize_guards_its_arguments()
    {
        Should.Throw<ArgumentNullException>(() => TargetingNormalizer.Normalize(null!, VariationOrder));
        Should.Throw<ArgumentNullException>(() => TargetingNormalizer.Normalize(Config(), null!));
    }
}
