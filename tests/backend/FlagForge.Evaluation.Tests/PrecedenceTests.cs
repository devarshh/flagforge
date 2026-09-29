using static FlagForge.Evaluation.Tests.Build;

namespace FlagForge.Evaluation.Tests;

public sealed class PrecedenceTests
{
    private static readonly Rule MatchesEveryone = Rule("r_everyone", Serve.Variation("false"), Clause("key", ClauseOperator.Exists));

    [Fact]
    public void Disabled_flag_serves_the_off_variation_even_for_targeted_keys()
    {
        var config = Config(enabled: false, offVariationId: "false", targets: [Target("true", "alice")], rules: [MatchesEveryone]);

        var result = Evaluate(config, Context("alice"));

        result.VariationId.ShouldBe("false");
        result.Value!.Value.GetBoolean().ShouldBeFalse();
        result.Reason.ShouldBe(EvaluationReason.Off);
    }

    [Fact]
    public void Individual_targets_beat_rules()
    {
        var config = Config(targets: [Target("true", "alice", "qa-bot")], rules: [MatchesEveryone]);

        var result = Evaluate(config, Context("alice"));

        result.VariationId.ShouldBe("true");
        result.Reason.ShouldBe(EvaluationReason.TargetMatch);
    }

    [Fact]
    public void The_first_target_list_containing_a_key_wins()
    {
        var config = Config(targets: [Target("false", "alice"), Target("true", "alice")]);

        Evaluate(config, Context("alice")).VariationId.ShouldBe("false");
    }

    [Fact]
    public void The_first_matching_rule_wins_and_reports_its_id_and_index()
    {
        var neverMatches = Rule("r_never", Serve.Variation("false"), Clause("plan", ClauseOperator.In, "enterprise"));
        var first = Rule("r_first", Serve.Variation("true"), Clause("plan", ClauseOperator.In, "premium"));
        var second = Rule("r_second", Serve.Variation("false"), Clause("plan", ClauseOperator.Exists));
        var config = Config(rules: [neverMatches, first, second]);

        var result = Evaluate(config, Context("bob", ("plan", AttributeValue.FromString("premium"))));

        result.VariationId.ShouldBe("true");
        result.Reason.ShouldBe(EvaluationReason.RuleMatch("r_first", 1, inRollout: false));
    }

    [Fact]
    public void Fallthrough_applies_when_nothing_matches()
    {
        var rule = Rule("r1", Serve.Variation("true"), Clause("plan", ClauseOperator.In, "premium"));
        var config = Config(targets: [Target("true", "alice")], rules: [rule], fallthrough: Serve.Variation("false"));

        var result = Evaluate(config, Context("bob"));

        result.VariationId.ShouldBe("false");
        result.Reason.ShouldBe(EvaluationReason.Fallthrough(inRollout: false));
    }

    [Fact]
    public void Rule_serving_a_rollout_reports_in_rollout()
    {
        var rule = Rule("r1", Rollout(("true", 100_000), ("false", 0)), Clause("key", ClauseOperator.Exists));

        var result = Evaluate(Config(rules: [rule]), Context("bob"));

        result.VariationId.ShouldBe("true");
        result.Reason.ShouldBe(EvaluationReason.RuleMatch("r1", 0, inRollout: true));
    }

    [Fact]
    public void Fallthrough_rollout_reports_in_rollout()
    {
        var result = Evaluate(Config(fallthrough: Rollout(("true", 0), ("false", 100_000))), Context("bob"));

        result.VariationId.ShouldBe("false");
        result.Reason.ShouldBe(EvaluationReason.Fallthrough(inRollout: true));
    }

    [Fact]
    public void Result_carries_the_flag_key_and_the_variation_json_value()
    {
        FlagVariation[] variations = [new("v_red", Json("\"red\"")), new("v_theme", Json("""{"accent":"#5E7F4F","rounded":true}"""))];
        var config = new TargetingConfig { Enabled = true, OffVariationId = "v_red", Fallthrough = Serve.Variation("v_theme") };
        var flag = CompiledFlag.Compile("store-theme", Salt, variations, config);

        var result = FlagEvaluator.Evaluate("store-theme", flag, Context("bob"));

        result.FlagKey.ShouldBe("store-theme");
        result.VariationId.ShouldBe("v_theme");
        result.Value!.Value.GetProperty("accent").GetString().ShouldBe("#5E7F4F");
        flag.Variations.Count.ShouldBe(2);
    }
}
