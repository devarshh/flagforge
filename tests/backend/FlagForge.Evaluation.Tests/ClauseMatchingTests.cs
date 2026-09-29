using static FlagForge.Evaluation.Tests.Build;

namespace FlagForge.Evaluation.Tests;

public sealed class ClauseMatchingTests
{
    /// <summary>Attribute value (as a JSON literal), operator, clause values, expected match.</summary>
    public static TheoryData<string, ClauseOperator, string[], bool> OperatorCases => new()
    {
        // in: strings use ordinal (case-sensitive) equality against any value
        { "\"premium\"", ClauseOperator.In, ["premium", "enterprise"], true },
        { "\"enterprise\"", ClauseOperator.In, ["premium", "enterprise"], true },
        { "\"free\"", ClauseOperator.In, ["premium", "enterprise"], false },
        { "\"Premium\"", ClauseOperator.In, ["premium"], false },
        { "\"\"", ClauseOperator.In, [""], true },

        // in: numbers compare numerically with values parsed as decimals (invariant culture)
        { "5", ClauseOperator.In, ["5"], true },
        { "5", ClauseOperator.In, ["5.0"], true },
        { "5.5", ClauseOperator.In, ["5.50", "7"], true },
        { "1000", ClauseOperator.In, ["1e3"], true },
        { "5", ClauseOperator.In, ["6", "five"], false },
        { "5", ClauseOperator.In, ["5,0"], false },

        // in: booleans match "true"/"false" case-insensitively
        { "true", ClauseOperator.In, ["true"], true },
        { "true", ClauseOperator.In, ["TRUE"], true },
        { "false", ClauseOperator.In, ["False"], true },
        { "false", ClauseOperator.In, ["true"], false },
        { "true", ClauseOperator.In, ["yes", "1"], false },

        // in: type mismatches never match
        { "\"5\"", ClauseOperator.In, ["5.0"], false },
        { "\"true\"", ClauseOperator.In, ["TRUE"], false },

        // contains / startsWith / endsWith: ordinal, strings only
        { "\"sam@acme.com\"", ClauseOperator.Contains, ["acme"], true },
        { "\"sam@acme.com\"", ClauseOperator.Contains, ["ACME"], false },
        { "\"sam@acme.com\"", ClauseOperator.Contains, ["nope", "@"], true },
        { "\"sam@acme.com\"", ClauseOperator.StartsWith, ["sam@"], true },
        { "\"sam@acme.com\"", ClauseOperator.StartsWith, ["Sam@"], false },
        { "\"sam@acme.com\"", ClauseOperator.EndsWith, ["@acme.com"], true },
        { "\"sam@acme.com\"", ClauseOperator.EndsWith, ["@acme.org", "@example.com"], false },
        { "123", ClauseOperator.Contains, ["2"], false },
        { "123", ClauseOperator.StartsWith, ["1"], false },
        { "true", ClauseOperator.EndsWith, ["e"], false },

        // lt / lte / gt / gte: numbers only, against values parsed as decimals
        { "17", ClauseOperator.Lt, ["18"], true },
        { "18", ClauseOperator.Lt, ["18"], false },
        { "18", ClauseOperator.Lte, ["18"], true },
        { "18.001", ClauseOperator.Lte, ["18"], false },
        { "19", ClauseOperator.Gt, ["18"], true },
        { "18", ClauseOperator.Gt, ["18"], false },
        { "18", ClauseOperator.Gte, ["18"], true },
        { "-2.5", ClauseOperator.Gte, ["-2.4"], false },
        { "10", ClauseOperator.Lt, ["5", "20"], true },
        { "10", ClauseOperator.Gt, ["50", "3"], true },
        { "10", ClauseOperator.Gt, ["not-a-number"], false },
        { "\"17\"", ClauseOperator.Lt, ["18"], false },
        { "true", ClauseOperator.Gt, ["0"], false },

        // exists: any present value
        { "\"x\"", ClauseOperator.Exists, [], true },
        { "0", ClauseOperator.Exists, [], true },
        { "false", ClauseOperator.Exists, [], true },
        { "[]", ClauseOperator.Exists, [], true },
    };

    [Theory]
    [MemberData(nameof(OperatorCases))]
    public void Operator_matches_scalar_values(string attributeJson, ClauseOperator op, string[] values, bool expected)
    {
        var clause = Clause("attr", op, values);

        Matches(clause, ContextWith("attr", attributeJson)).ShouldBe(expected);
    }

    [Theory]
    [MemberData(nameof(OperatorCases))]
    public void Negate_inverts_the_match_when_the_attribute_is_present(string attributeJson, ClauseOperator op, string[] values, bool expected)
    {
        var clause = Not(Clause("attr", op, values));

        Matches(clause, ContextWith("attr", attributeJson)).ShouldBe(!expected);
    }

    [Theory]
    [InlineData(ClauseOperator.In)]
    [InlineData(ClauseOperator.Contains)]
    [InlineData(ClauseOperator.StartsWith)]
    [InlineData(ClauseOperator.EndsWith)]
    [InlineData(ClauseOperator.Lt)]
    [InlineData(ClauseOperator.Lte)]
    [InlineData(ClauseOperator.Gt)]
    [InlineData(ClauseOperator.Gte)]
    public void Missing_attribute_never_matches_even_when_negated(ClauseOperator op)
    {
        var context = Context("user-1");

        Matches(Clause("plan", op, "premium", "5"), context).ShouldBeFalse();
        Matches(Not(Clause("plan", op, "premium", "5")), context).ShouldBeFalse();
    }

    [Fact]
    public void Null_attribute_behaves_like_a_missing_attribute()
    {
        var context = ContextWith("plan", "null");

        Matches(Clause("plan", ClauseOperator.In, "premium"), context).ShouldBeFalse();
        Matches(Not(Clause("plan", ClauseOperator.In, "premium")), context).ShouldBeFalse();
        Matches(Clause("plan", ClauseOperator.Exists), context).ShouldBeFalse();
        Matches(Not(Clause("plan", ClauseOperator.Exists)), context).ShouldBeTrue();
    }

    [Fact]
    public void Exists_and_negated_exists_check_presence()
    {
        var withPlan = Context("user-1", ("plan", AttributeValue.FromString("free")));
        var withoutPlan = Context("user-2");

        Matches(Clause("plan", ClauseOperator.Exists), withPlan).ShouldBeTrue();
        Matches(Clause("plan", ClauseOperator.Exists), withoutPlan).ShouldBeFalse();
        Matches(Not(Clause("plan", ClauseOperator.Exists)), withPlan).ShouldBeFalse();
        Matches(Not(Clause("plan", ClauseOperator.Exists)), withoutPlan).ShouldBeTrue();
    }

    [Fact]
    public void Attribute_names_are_case_sensitive()
    {
        var context = Context("user-1", ("Plan", AttributeValue.FromString("premium")));

        Matches(Clause("plan", ClauseOperator.In, "premium"), context).ShouldBeFalse();
        Matches(Clause("Plan", ClauseOperator.In, "premium"), context).ShouldBeTrue();
    }

    [Theory]
    [InlineData("[\"staff\", \"qa\"]", ClauseOperator.In, new[] { "qa" }, true)]
    [InlineData("[\"staff\"]", ClauseOperator.In, new[] { "qa", "beta" }, false)]
    [InlineData("[]", ClauseOperator.In, new[] { "qa" }, false)]
    [InlineData("[1, 50]", ClauseOperator.Gt, new[] { "40" }, true)]
    [InlineData("[1, 5]", ClauseOperator.Gt, new[] { "40" }, false)]
    [InlineData("[\"a\", 5, true]", ClauseOperator.In, new[] { "5" }, true)]
    [InlineData("[\"a\", 5, true]", ClauseOperator.In, new[] { "TRUE" }, true)]
    [InlineData("[\"sam@example.com\", \"sam@acme.com\"]", ClauseOperator.EndsWith, new[] { "@acme.com" }, true)]
    [InlineData("[1, 2]", ClauseOperator.Contains, new[] { "1" }, false)]
    public void Array_attributes_match_when_any_element_matches(string attributeJson, ClauseOperator op, string[] values, bool expected)
    {
        Matches(Clause("groups", op, values), ContextWith("groups", attributeJson)).ShouldBe(expected);
    }

    [Fact]
    public void Negated_array_clause_inverts_the_any_element_result()
    {
        var staffAndQa = ContextWith("groups", "[\"staff\", \"qa\"]");
        var empty = ContextWith("groups", "[]");

        Matches(Not(Clause("groups", ClauseOperator.In, "staff")), staffAndQa).ShouldBeFalse();
        Matches(Not(Clause("groups", ClauseOperator.In, "admins")), staffAndQa).ShouldBeTrue();
        Matches(Not(Clause("groups", ClauseOperator.In, "staff")), empty).ShouldBeTrue();
    }

    [Theory]
    [InlineData(ClauseOperator.In, new[] { "alice", "bob" }, true)]
    [InlineData(ClauseOperator.In, new[] { "Alice" }, false)]
    [InlineData(ClauseOperator.StartsWith, new[] { "ali" }, true)]
    [InlineData(ClauseOperator.EndsWith, new[] { "ce" }, true)]
    [InlineData(ClauseOperator.Contains, new[] { "lic" }, true)]
    [InlineData(ClauseOperator.Exists, new string[0], true)]
    [InlineData(ClauseOperator.Gt, new[] { "0" }, false)]
    public void Key_attribute_addresses_the_context_key(ClauseOperator op, string[] values, bool expected)
    {
        Matches(Clause("key", op, values), Context("alice")).ShouldBe(expected);
    }

    [Fact]
    public void Rule_matches_only_when_every_clause_matches()
    {
        var context = Context(
            "user-1",
            ("email", AttributeValue.FromString("sam@acme.com")),
            ("plan", AttributeValue.FromString("free")));
        var bothMatch = Rule("r1", Serve.Variation("true"), Clause("email", ClauseOperator.EndsWith, "@acme.com"), Clause("plan", ClauseOperator.In, "free"));
        var oneMatches = Rule("r1", Serve.Variation("true"), Clause("email", ClauseOperator.EndsWith, "@acme.com"), Clause("plan", ClauseOperator.In, "premium"));

        Evaluate(Config(rules: [bothMatch]), context).Reason.Kind.ShouldBe(EvaluationReasonKind.RuleMatch);
        Evaluate(Config(rules: [oneMatches]), context).Reason.Kind.ShouldBe(EvaluationReasonKind.Fallthrough);
    }

    private static bool Matches(Clause clause, EvaluationContext context)
    {
        var config = Config(rules: [Rule("r_test", Serve.Variation("true"), clause)]);
        return Evaluate(config, context).Reason.Kind == EvaluationReasonKind.RuleMatch;
    }
}
