using System.Text.Json;
using System.Text.Json.Serialization;
using static FlagForge.Evaluation.Tests.Build;

namespace FlagForge.Evaluation.Tests;

/// <summary>Deserializes the targeting example from the specification (§7.1) and evaluates it.</summary>
public sealed class SpecExampleTests
{
    private const string SpecConfigJson = """
        {
          "enabled": true,
          "offVariationId": "false",
          "targets": [
            { "variationId": "true", "contextKeys": ["alice", "qa-bot"] }
          ],
          "rules": [
            {
              "id": "r_7f3a2c",
              "description": "Internal staff",
              "clauses": [
                { "attribute": "email", "operator": "endsWith", "values": ["@acme.com"], "negate": false }
              ],
              "serve": { "variationId": "true" }
            },
            {
              "id": "r_91bc04",
              "description": "Gradual rollout for paid plans",
              "clauses": [
                { "attribute": "plan", "operator": "in", "values": ["premium", "enterprise"], "negate": false }
              ],
              "serve": {
                "rollout": {
                  "bucketBy": "key",
                  "weights": [
                    { "variationId": "true", "weight": 25000 },
                    { "variationId": "false", "weight": 75000 }
                  ]
                }
              }
            }
          ],
          "fallthrough": { "variationId": "false" },
          "version": 7
        }
        """;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private static readonly TargetingConfig SpecConfig = JsonSerializer.Deserialize<TargetingConfig>(SpecConfigJson, Options)!;

    [Fact]
    public void Spec_example_deserializes_and_validates()
    {
        SpecConfig.Rules.Count.ShouldBe(2);
        SpecConfig.Rules[1].Serve.Rollout!.Weights[0].Weight.ShouldBe(25_000);
        TargetingValidator.Validate(SpecConfig, BooleanVariationIds).ShouldBeEmpty();
    }

    [Fact]
    public void Targeted_key_matches_the_target_list()
    {
        Evaluate(SpecConfig, Context("qa-bot")).Reason.ShouldBe(EvaluationReason.TargetMatch);
    }

    [Fact]
    public void Staff_email_matches_the_first_rule()
    {
        var result = Evaluate(SpecConfig, Context("sam", ("email", AttributeValue.FromString("sam@acme.com"))));

        result.VariationId.ShouldBe("true");
        result.Reason.ShouldBe(EvaluationReason.RuleMatch("r_7f3a2c", 0, inRollout: false));
    }

    [Fact]
    public void Paid_plan_matches_the_rollout_rule()
    {
        var result = Evaluate(SpecConfig, Context("user-123", ("plan", AttributeValue.FromString("premium"))));

        var expected = Bucketing.ComputeBucket(FlagKey, Salt, "user-123") < 25_000 ? "true" : "false";
        result.VariationId.ShouldBe(expected);
        result.Reason.ShouldBe(EvaluationReason.RuleMatch("r_91bc04", 1, inRollout: true));
    }

    [Fact]
    public void Free_plan_falls_through()
    {
        var result = Evaluate(SpecConfig, Context("bob", ("plan", AttributeValue.FromString("free"))));

        result.VariationId.ShouldBe("false");
        result.Reason.ShouldBe(EvaluationReason.Fallthrough(inRollout: false));
    }

    [Fact]
    public void Unknown_operator_is_rejected_when_reading_json()
    {
        var json = SpecConfigJson.Replace("\"endsWith\"", "\"matches\"", StringComparison.Ordinal);

        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<TargetingConfig>(json, Options));
    }

    [Fact]
    public void A_serve_writes_only_the_alternative_it_uses()
    {
        JsonSerializer.Serialize(SpecConfig.Rules[0].Serve, Options).ShouldBe("""{"variationId":"true"}""");

        using var rollout = JsonDocument.Parse(JsonSerializer.Serialize(SpecConfig.Rules[1].Serve, Options));
        rollout.RootElement.EnumerateObject().Select(property => property.Name).ShouldBe(["rollout"]);
    }
}
