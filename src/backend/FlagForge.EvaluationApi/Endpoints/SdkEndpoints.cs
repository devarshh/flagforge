using System.Security.Claims;
using System.Text.Json;
using FlagForge.Application.Common;
using FlagForge.Application.Evaluations;
using FlagForge.Evaluation;
using FlagForge.EvaluationApi.Authentication;
using FlagForge.EvaluationApi.Hubs;
using FlagForge.EvaluationApi.Snapshots;
using FlagForge.EvaluationApi.Usage;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FlagForge.EvaluationApi.Endpoints;

public sealed record EvaluateRequest(JsonElement Context);

/// <summary>Values, variations, and reasons for every non-archived flag. Never contains targeting configuration.</summary>
public sealed record EvaluateAllResponse(long EnvironmentVersion, IReadOnlyDictionary<string, FlagValueResponse> Flags);

internal static class SdkEndpoints
{
    public const string RateLimitPolicy = "sdk";
    public const string CorsPolicy = "sdk";

    public static WebApplication MapSdkEndpoints(this WebApplication app)
    {
        var sdk = app.MapGroup("/sdk/v1")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicy)
            .RequireCors(CorsPolicy)
            .WithTags("SDK")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesValidationProblem();
        sdk.MapPost("/evaluate", EvaluateAllAsync).WithName("EvaluateAll");
        sdk.MapPost("/evaluate/{flagKey}", EvaluateOneAsync).WithName("EvaluateFlag").ProducesProblem(StatusCodes.Status404NotFound);

        // WebSockets only: without negotiation there is no second request that must reach the same pod (ADR 0004).
        app.MapHub<FlagsHub>(FlagsHub.Path, options => options.Transports = HttpTransportType.WebSockets).RequireCors(CorsPolicy);
        return app;
    }

    private static async Task<Ok<EvaluateAllResponse>> EvaluateAllAsync(
        EvaluateRequest request,
        ClaimsPrincipal user,
        SnapshotCache snapshots,
        UsageAggregator usage,
        EvaluationMetrics metrics,
        CancellationToken cancellationToken)
    {
        var context = ParseContext(request);
        var environmentId = user.GetEnvironmentId();
        var snapshot = await GetSnapshotAsync(snapshots, environmentId, cancellationToken);
        var flags = new Dictionary<string, FlagValueResponse>(snapshot.Flags.Count, StringComparer.Ordinal);
        foreach (var (key, flag) in snapshot.Flags)
        {
            var result = FlagEvaluator.Evaluate(key, flag.Compiled, context);
            flags[key] = FlagValueResponse.From(result);
            usage.Record(environmentId, flag.Id, result.VariationId);
        }

        metrics.RecordEvaluations(environmentId, flags.Count);
        return TypedResults.Ok(new EvaluateAllResponse(snapshot.ConfigVersion, flags));
    }

    private static async Task<Ok<EvaluationResultResponse>> EvaluateOneAsync(
        string flagKey,
        EvaluateRequest request,
        ClaimsPrincipal user,
        SnapshotCache snapshots,
        UsageAggregator usage,
        EvaluationMetrics metrics,
        CancellationToken cancellationToken)
    {
        var context = ParseContext(request);
        var environmentId = user.GetEnvironmentId();
        var snapshot = await GetSnapshotAsync(snapshots, environmentId, cancellationToken);
        if (!snapshot.Flags.TryGetValue(flagKey, out var flag))
        {
            throw new NotFoundException($"Flag '{flagKey}' does not exist or is archived.");
        }

        var result = FlagEvaluator.Evaluate(flagKey, flag.Compiled, context);
        usage.Record(environmentId, flag.Id, result.VariationId);
        metrics.RecordEvaluations(environmentId, 1);
        return TypedResults.Ok(EvaluationResultResponse.From(result));
    }

    private static EvaluationContext ParseContext(EvaluateRequest request)
    {
        if (!EvaluationContextParser.TryParse(request.Context, out var context, out var errors))
        {
            errors.ThrowIfAny("context");
        }

        return context!;
    }

    private static async Task<EnvironmentSnapshot> GetSnapshotAsync(SnapshotCache snapshots, Guid environmentId, CancellationToken cancellationToken) =>
        await snapshots.GetAsync(environmentId, cancellationToken)
        ?? throw new NotFoundException("The environment of this SDK key no longer exists.");
}
