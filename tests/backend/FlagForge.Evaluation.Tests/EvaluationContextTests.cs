using static FlagForge.Evaluation.Tests.Build;

namespace FlagForge.Evaluation.Tests;

public sealed class EvaluationContextTests
{
    [Fact]
    public void Key_attribute_resolves_to_the_context_key()
    {
        var context = Context("alice", ("plan", AttributeValue.FromString("free")));

        context.GetAttribute("key")!.StringValue.ShouldBe("alice");
        context.GetAttribute("plan")!.StringValue.ShouldBe("free");
        context.GetAttribute("missing").ShouldBeNull();
    }

    [Fact]
    public void Attribute_lookup_is_case_sensitive_even_if_the_caller_used_another_comparer()
    {
        var caseInsensitive = new Dictionary<string, AttributeValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["Plan"] = AttributeValue.FromString("premium"),
        };

        var context = new EvaluationContext("alice", caseInsensitive);

        context.GetAttribute("plan").ShouldBeNull();
        context.GetAttribute("Plan").ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_rejects_invalid_input()
    {
        Should.Throw<ArgumentException>(() => new EvaluationContext(""));
        Should.Throw<ArgumentException>(() => new EvaluationContext(
            "alice",
            new Dictionary<string, AttributeValue> { ["key"] = AttributeValue.FromString("x") }));
    }

    [Fact]
    public void Array_values_only_contain_scalars()
    {
        var array = AttributeValue.FromArray([AttributeValue.FromNumber(1)]);

        array.Kind.ShouldBe(AttributeValueKind.Array);
        Should.Throw<ArgumentException>(() => AttributeValue.FromArray([array]));
        Should.Throw<ArgumentNullException>(() => AttributeValue.FromArray([null!]));
        Should.Throw<ArgumentNullException>(() => AttributeValue.FromArray(null!));
        Should.Throw<ArgumentNullException>(() => AttributeValue.FromString(null!));
    }

    [Fact]
    public void Boolean_values_are_shared_instances()
    {
        AttributeValue.FromBoolean(true).ShouldBeSameAs(AttributeValue.True);
        AttributeValue.FromBoolean(false).ShouldBeSameAs(AttributeValue.False);
    }

    [Fact]
    public void Validation_paths_combine_names_and_indexes()
    {
        ValidationPath.Combine("", "key").ShouldBe("key");
        ValidationPath.Combine("context", "").ShouldBe("context");
        ValidationPath.Combine("context", "attributes.plan").ShouldBe("context.attributes.plan");
        ValidationPath.Combine("rules", "[0]").ShouldBe("rules[0]");
        ValidationPath.Index("rules", 3).ShouldBe("rules[3]");
    }
}
