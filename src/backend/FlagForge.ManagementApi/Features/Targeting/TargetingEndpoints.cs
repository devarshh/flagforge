using System.Security.Claims;
using FlagForge.Application.Evaluations;
using FlagForge.Application.Targeting;
using FlagForge.ManagementApi.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FlagForge.ManagementApi.Features.Targeting;

internal static class TargetingEndpoints
{
    public static RouteGroupBuilder MapTargetingEndpoints(this RouteGroupBuilder api)
    {
        var targeting = api.MapGroup("/projects/{projectKey}/flags/{flagKey}/environments/{environmentKey}")
            .WithTags("Targeting")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        targeting.MapGet("/", GetAsync)
            .RequireAuthorization(Policies.CanRead)
            .WithName("GetTargeting")
            .WithSummary("Get a flag's targeting in one environment, with its version");
        targeting.MapPut("/", UpdateAsync)
            .RequireAuthorization(Policies.CanEdit)
            .WithName("UpdateTargeting")
            .WithSummary("Replace the targeting; send expectedVersion (409 with currentVersion when stale). Protected environments need an admin and a comment")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
        targeting.MapPost("/toggle", ToggleAsync)
            .RequireAuthorization(Policies.CanEdit)
            .WithName("ToggleFlag")
            .WithSummary("Turn the flag on or off in this environment")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
        targeting.MapPost("/evaluate-preview", PreviewAsync)
            .RequireAuthorization(Policies.CanRead)
            .WithName("PreviewEvaluation")
            .WithSummary("Evaluate a context against the saved or a draft config (not counted as usage)")
            .ProducesValidationProblem();
        return api;
    }

    private static async Task<Ok<TargetingResponse>> GetAsync(
        string projectKey, string flagKey, string environmentKey, TargetingService targeting, CancellationToken cancellationToken) =>
        TypedResults.Ok(await targeting.GetAsync(projectKey, flagKey, environmentKey, cancellationToken));

    private static async Task<Ok<TargetingResponse>> UpdateAsync(
        string projectKey,
        string flagKey,
        string environmentKey,
        UpdateTargetingRequest request,
        TargetingService targeting,
        ClaimsPrincipal user,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await targeting.UpdateAsync(projectKey, flagKey, environmentKey, request, user.ToActor(), cancellationToken));

    private static async Task<Ok<TargetingResponse>> ToggleAsync(
        string projectKey,
        string flagKey,
        string environmentKey,
        ToggleRequest request,
        TargetingService targeting,
        ClaimsPrincipal user,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await targeting.ToggleAsync(projectKey, flagKey, environmentKey, request, user.ToActor(), cancellationToken));

    private static async Task<Ok<EvaluationResultResponse>> PreviewAsync(
        string projectKey, string flagKey, string environmentKey, PreviewRequest request, TargetingService targeting, CancellationToken cancellationToken) =>
        TypedResults.Ok(await targeting.PreviewAsync(projectKey, flagKey, environmentKey, request, cancellationToken));
}
