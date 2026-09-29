using static FlagForge.Evaluation.Tests.Build;

namespace FlagForge.Evaluation.Tests;

public sealed class RolloutTests
{
    private const int SyntheticKeys = 20_000;

    /// <summary>Values computed independently with Python's hashlib, pinning the cross-language algorithm.</summary>
    [Theory]
    [InlineData("new-product-layout", "a1b2c3d4e5f60718", "user-123", 87787)]
    [InlineData("new-product-layout", "a1b2c3d4e5f60718", "alice", 86296)]
    [InlineData("promo-banner", "0011223344556677", "31", 45000)]
    [InlineData("store-theme", "ffffffffffffffff", "café", 62543)]
    public void Bucket_matches_golden_vectors(string flagKey, string salt, string bucketValue, int expected)
    {
        Bucketing.ComputeBucket(flagKey, salt, bucketValue).ShouldBe(expected);
    }

    [Fact]
    public void Bucket_matches_golden_vector_for_input_larger_than_the_stack_buffer()
    {
        Bucketing.ComputeBucket("long-flag", "0000000000000000", new string('x', 1000)).ShouldBe(49857);
    }

    [Fact]
    public void Bucketing_is_deterministic()
    {
        var config = Config(fallthrough: Rollout(("true", 50_000), ("false", 50_000)));
        var flag = BooleanFlag(config);

        for (var i = 0; i < 200; i++)
        {
            var context = Context($"user-{i}");
            var first = FlagEvaluator.Evaluate(FlagKey, flag, context);
            var recompiled = FlagEvaluator.Evaluate(FlagKey, BooleanFlag(config), context);
            recompiled.ShouldBe(first);
            Bucketing.ComputeBucket(FlagKey, Salt, $"user-{i}").ShouldBe(Bucketing.ComputeBucket(FlagKey, Salt, $"user-{i}"));
        }
    }

    [Theory]
    [InlineData("flag-a", "0123456789abcdef", "flag-b", "0123456789abcdef")]
    [InlineData("flag-a", "0123456789abcdef", "flag-a", "fedcba9876543210")]
    public void Buckets_are_independent_across_flag_keys_and_salts(string keyA, string saltA, string keyB, string saltB)
    {
        // For independent buckets, P(A in lower half AND B in lower half) is 25%.
        var both = 0;
        for (var i = 0; i < SyntheticKeys; i++)
        {
            var value = $"user-{i}";
            if (Bucketing.ComputeBucket(keyA, saltA, value) < 50_000 && Bucketing.ComputeBucket(keyB, saltB, value) < 50_000)
            {
                both++;
            }
        }

        var share = (double)both / SyntheticKeys;
        share.ShouldBeInRange(0.235, 0.265);
    }

    [Fact]
    public void Ramping_up_is_monotonic_because_true_comes_first()
    {
        int[] steps = [5_000, 10_000, 20_000, 25_000, 50_000, 100_000];
        var servedTrue = new HashSet<string>();
        foreach (var weight in steps)
        {
            var flag = BooleanFlag(Config(fallthrough: Rollout(("true", weight), ("false", 100_000 - weight))));
            var nowTrue = Enumerable.Range(0, 5_000)
                .Select(i => $"user-{i}")
                .Where(key => FlagEvaluator.Evaluate(FlagKey, flag, Context(key)).VariationId == "true")
                .ToHashSet();

            nowTrue.IsSupersetOf(servedTrue).ShouldBeTrue($"a context served true below {weight} lost it at {weight}");
            servedTrue = nowTrue;
        }

        servedTrue.Count.ShouldBe(5_000);
    }

    [Theory]
    [InlineData(new[] { 25_000, 75_000 })]
    [InlineData(new[] { 50_000, 50_000 })]
    [InlineData(new[] { 10_000, 30_000, 60_000 })]
    [InlineData(new[] { 33_334, 33_333, 33_333 })]
    public void Distribution_is_within_one_and_a_half_points_of_the_weights(int[] weights)
    {
        FlagVariation[] variations = [.. weights.Select((_, i) => new FlagVariation($"v_{i}", Json(i.ToString(System.Globalization.CultureInfo.InvariantCulture))))];
        var config = new TargetingConfig
        {
            Enabled = true,
            OffVariationId = "v_0",
            Fallthrough = Rollout([.. weights.Select((w, i) => ($"v_{i}", w))]),
        };
        var flag = CompiledFlag.Compile("distribution-flag", "5eed5eed5eed5eed", variations, config);

        var counts = new Dictionary<string, int>();
        for (var i = 0; i < SyntheticKeys; i++)
        {
            var result = FlagEvaluator.Evaluate("distribution-flag", flag, Context($"synthetic-{i}"));
            counts[result.VariationId!] = counts.GetValueOrDefault(result.VariationId!) + 1;
        }

        for (var i = 0; i < weights.Length; i++)
        {
            var expectedPercent = weights[i] / 1000.0;
            var actualPercent = 100.0 * counts.GetValueOrDefault($"v_{i}") / SyntheticKeys;
            actualPercent.ShouldBeInRange(expectedPercent - 1.5, expectedPercent + 1.5);
        }
    }

    [Theory]
    [InlineData("company")]
    [InlineData("beta")]
    [InlineData("groups")]
    public void Missing_boolean_or_array_bucket_by_attribute_uses_bucket_zero(string bucketBy)
    {
        // Bucket 0 always falls into the first variation with a non-zero weight.
        var config = Config(fallthrough: RolloutBy(bucketBy, ("true", 1), ("false", 99_999)));
        var flag = BooleanFlag(config);

        for (var i = 0; i < 50; i++)
        {
            var context = Context(
                $"user-{i}",
                ("beta", AttributeValue.True),
                ("groups", AttributeValue.FromArray([AttributeValue.FromString("staff")])));
            Bucketing.ComputeBucket(FlagKey, Salt, context, bucketBy).ShouldBe(0);
            FlagEvaluator.Evaluate(FlagKey, flag, context).VariationId.ShouldBe("true");
        }
    }

    [Fact]
    public void Zero_weight_variations_are_never_served()
    {
        var flag = BooleanFlag(Config(fallthrough: Rollout(("true", 0), ("false", 100_000))));

        for (var i = 0; i < 500; i++)
        {
            FlagEvaluator.Evaluate(FlagKey, flag, Context($"user-{i}")).VariationId.ShouldBe("false");
        }
    }

    [Fact]
    public void Rollout_can_bucket_by_a_string_attribute()
    {
        var flag = BooleanFlag(Config(fallthrough: RolloutBy("company", ("true", 50_000), ("false", 50_000))));
        var expected = Bucketing.ComputeBucket(FlagKey, Salt, "acme") < 50_000 ? "true" : "false";

        for (var i = 0; i < 20; i++)
        {
            var context = Context($"user-{i}", ("company", AttributeValue.FromString("acme")));
            FlagEvaluator.Evaluate(FlagKey, flag, context).VariationId.ShouldBe(expected);
        }
    }

    [Theory]
    [InlineData("31", "31")]
    [InlineData("31.0", "31")]
    [InlineData("0.50", "0.5")]
    [InlineData("-2.500", "-2.5")]
    [InlineData("1e3", "1000")]
    public void Number_bucket_values_use_invariant_culture_without_trailing_zeros(string json, string expected)
    {
        var context = ContextWith("age", json);

        Bucketing.GetBucketValue(context.GetAttribute("age")).ShouldBe(expected);
        Bucketing.ComputeBucket(FlagKey, Salt, context, "age").ShouldBe(Bucketing.ComputeBucket(FlagKey, Salt, expected));
    }

    [Fact]
    public void Under_allocated_weights_fall_back_to_the_last_weight()
    {
        // The validator rejects this config; the engine still serves a variation instead of failing.
        var flag = BooleanFlag(Config(fallthrough: Rollout(("true", 0), ("false", 0))));

        FlagEvaluator.Evaluate(FlagKey, flag, Context("anyone")).VariationId.ShouldBe("false");
    }
}
