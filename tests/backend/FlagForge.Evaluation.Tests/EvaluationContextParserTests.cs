using System.Globalization;
using static FlagForge.Evaluation.Tests.Build;

namespace FlagForge.Evaluation.Tests;

public sealed class EvaluationContextParserTests
{
    [Fact]
    public void Parses_every_supported_attribute_type()
    {
        var json = Json("""
            {
              "key": "user-123",
              "attributes": {
                "email": "sam@acme.com",
                "plan": "premium",
                "age": 31,
                "beta": true,
                "optOut": false,
                "groups": ["staff", "qa"],
                "nickname": null
              },
              "ignored": "unknown top-level properties are ignored"
            }
            """);

        EvaluationContextParser.TryParse(json, out var context, out var errors).ShouldBeTrue();

        errors.ShouldBeEmpty();
        context.Key.ShouldBe("user-123");
        context.Attributes.Count.ShouldBe(6);
        context.GetAttribute("email")!.StringValue.ShouldBe("sam@acme.com");
        context.GetAttribute("age")!.NumberValue.ShouldBe(31m);
        context.GetAttribute("beta")!.BooleanValue.ShouldBeTrue();
        context.GetAttribute("optOut")!.BooleanValue.ShouldBeFalse();
        context.GetAttribute("groups")!.Items.Select(i => i.StringValue).ShouldBe(["staff", "qa"]);
        context.GetAttribute("nickname").ShouldBeNull();
    }

    [Theory]
    [InlineData("""{"key":"k"}""")]
    [InlineData("""{"key":"k","attributes":null}""")]
    public void Attributes_are_optional(string raw)
    {
        EvaluationContextParser.TryParse(Json(raw), out var context, out _).ShouldBeTrue();

        context.Attributes.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("\"just a string\"", "")]
    [InlineData("""{"attributes":{}}""", "key")]
    [InlineData("""{"key":null}""", "key")]
    [InlineData("""{"key":42}""", "key")]
    [InlineData("""{"key":""}""", "key")]
    [InlineData("""{"key":"k","attributes":[]}""", "attributes")]
    [InlineData("""{"key":"k","attributes":{"key":"x"}}""", "attributes.key")]
    [InlineData("""{"key":"k","attributes":{"has space":"x"}}""", "attributes.has space")]
    [InlineData("""{"key":"k","attributes":{"1st":"x"}}""", "attributes.1st")]
    [InlineData("""{"key":"k","attributes":{"big":1e30}}""", "attributes.big")]
    [InlineData("""{"key":"k","attributes":{"obj":{"a":1}}}""", "attributes.obj")]
    [InlineData("""{"key":"k","attributes":{"list":["a",{"b":1}]}}""", "attributes.list[1]")]
    [InlineData("""{"key":"k","attributes":{"list":["a",null]}}""", "attributes.list[1]")]
    [InlineData("""{"key":"k","attributes":{"list":[["nested"]]}}""", "attributes.list[0]")]
    [InlineData("""{"key":"k","attributes":{"plan":"a","plan":"b"}}""", "attributes.plan")]
    public void Invalid_contexts_report_the_offending_path(string raw, string expectedPath)
    {
        EvaluationContextParser.TryParse(Json(raw), out var context, out var errors).ShouldBeFalse();

        context.ShouldBeNull();
        errors.ShouldContain(e => e.Path == expectedPath, string.Join(" | ", errors.Select(e => $"{e.Path}: {e.Message}")));
    }

    [Fact]
    public void Key_is_limited_to_256_characters()
    {
        EvaluationContextParser.TryParse(Json($$"""{"key":"{{new string('k', 256)}}"}"""), out _, out _).ShouldBeTrue();
        EvaluationContextParser.TryParse(Json($$"""{"key":"{{new string('k', 257)}}"}"""), out _, out var errors).ShouldBeFalse();

        errors.Single().Path.ShouldBe("key");
    }

    [Fact]
    public void Context_is_limited_to_50_attributes()
    {
        EvaluationContextParser.TryParse(Json(ContextWithAttributes(50)), out _, out _).ShouldBeTrue();
        EvaluationContextParser.TryParse(Json(ContextWithAttributes(51)), out _, out var errors).ShouldBeFalse();

        errors.Single().Path.ShouldBe("attributes");
    }

    [Fact]
    public void Text_values_are_limited_to_1024_characters()
    {
        EvaluationContextParser.TryParse(Json($$$"""{"key":"k","attributes":{"bio":"{{{new string('b', 1024)}}}"}}"""), out _, out _).ShouldBeTrue();
        EvaluationContextParser.TryParse(Json($$$"""{"key":"k","attributes":{"bio":"{{{new string('b', 1025)}}}"}}"""), out _, out var errors).ShouldBeFalse();
        EvaluationContextParser.TryParse(Json($$$"""{"key":"k","attributes":{"bios":["{{{new string('b', 1025)}}}"]}}"""), out _, out var arrayErrors).ShouldBeFalse();

        errors.Single().Path.ShouldBe("attributes.bio");
        arrayErrors.Single().Path.ShouldBe("attributes.bios[0]");
    }

    [Fact]
    public void Arrays_are_limited_to_100_items()
    {
        static string ArrayOf(int count) => $$$"""{"key":"k","attributes":{"list":[{{{string.Join(",", Enumerable.Range(0, count))}}}]}}""";

        EvaluationContextParser.TryParse(Json(ArrayOf(100)), out var context, out _).ShouldBeTrue();
        EvaluationContextParser.TryParse(Json(ArrayOf(101)), out _, out var errors).ShouldBeFalse();

        context!.GetAttribute("list")!.Items.Count.ShouldBe(100);
        errors.Single().Path.ShouldBe("attributes.list");
    }

    private static string ContextWithAttributes(int count)
    {
        var attributes = string.Join(",", Enumerable.Range(0, count).Select(i => $"\"a{i.ToString(CultureInfo.InvariantCulture)}\":{i}"));
        return $$$"""{"key":"k","attributes":{{{{attributes}}}}}""";
    }
}
