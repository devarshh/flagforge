using System.Net;
using System.Text.Json;
using FlagForge.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FlagForge.ManagementApi.IntegrationTests;

public sealed class HostTests(FlagForgeFixture fixture) : ManagementApiTest(fixture)
{
    [Fact]
    public async Task Every_api_endpoint_is_described_in_the_openapi_document()
    {
        using var client = AnonymousClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json", Ct));
        var paths = document.RootElement.GetProperty("paths");
        var endpoints = Api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText!.StartsWith("/api/v1", StringComparison.Ordinal))
            .SelectMany(e => e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Select(method => (Method: method, Path: OpenApiPath(e))))
            .ToList();

        endpoints.Count.ShouldBeGreaterThanOrEqualTo(45);
        foreach (var (method, path) in endpoints)
        {
            var documented = paths.EnumerateObject().FirstOrDefault(p => p.Name.TrimEnd('/') == path.TrimEnd('/'));
            documented.Value.ValueKind.ShouldBe(JsonValueKind.Object, $"{path} is missing from the OpenAPI document");
            documented.Value.TryGetProperty(method.ToLowerInvariant(), out _).ShouldBeTrue($"{method} {path} is missing from the OpenAPI document");
        }
    }

    [Fact]
    public async Task Health_endpoints_report_live_and_ready()
    {
        using var client = AnonymousClient();

        var live = await client.GetAsync("/health/live", Ct);
        var ready = await client.GetAsync("/health/ready", Ct);

        live.StatusCode.ShouldBe(HttpStatusCode.OK);
        ready.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ready.Content.ReadAsStringAsync(Ct)).ShouldBe("Healthy");
    }

    /// <summary>Route patterns carry constraints (<c>{userId:guid}</c>); OpenAPI paths do not.</summary>
    private static string OpenApiPath(RouteEndpoint endpoint) =>
        "/" + string.Join('/', endpoint.RoutePattern.PathSegments.Select(segment => string.Concat(segment.Parts.Select(part => part switch
        {
            Microsoft.AspNetCore.Routing.Patterns.RoutePatternParameterPart parameter => $"{{{parameter.Name}}}",
            Microsoft.AspNetCore.Routing.Patterns.RoutePatternLiteralPart literal => literal.Content,
            _ => string.Empty,
        }))));
}
