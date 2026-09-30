using System.Security.Claims;
using FlagForge.Application.Schedules;
using FlagForge.ManagementApi.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FlagForge.ManagementApi.Features.Schedules;

internal static class ScheduleEndpoints
{
    public static RouteGroupBuilder MapScheduleEndpoints(this RouteGroupBuilder api)
    {
        var schedules = api.MapGroup("/projects/{projectKey}/flags/{flagKey}/environments/{environmentKey}")
            .WithTags("Schedules")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        schedules.MapGet("/scheduled-changes", ListAsync)
            .RequireAuthorization(Policies.CanRead)
            .WithName("ListScheduledChanges")
            .WithSummary("List scheduled changes for a flag in one environment");
        schedules.MapGet("/scheduled-changes/{changeId:guid}", GetAsync)
            .RequireAuthorization(Policies.CanRead)
            .WithName("GetScheduledChange")
            .WithSummary("Get a scheduled change");
        schedules.MapPost("/scheduled-changes", CreateAsync)
            .RequireAuthorization(Policies.CanEdit)
            .WithName("CreateScheduledChange")
            .WithSummary("Schedule turning the flag on or off, or a new default rule")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
        schedules.MapPost("/release-plan", CreateReleasePlanAsync)
            .RequireAuthorization(Policies.CanEdit)
            .WithName("CreateReleasePlan")
            .WithSummary("Schedule a gradual rollout: 1–10 steps in ascending time")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
        schedules.MapDelete("/scheduled-changes/{changeId:guid}", CancelAsync)
            .RequireAuthorization(Policies.CanEdit)
            .WithName("CancelScheduledChange")
            .WithSummary("Cancel a pending scheduled change")
            .ProducesProblem(StatusCodes.Status409Conflict);
        return api;
    }

    private static async Task<Ok<IReadOnlyList<ScheduledChangeResponse>>> ListAsync(
        string projectKey, string flagKey, string environmentKey, ScheduleService schedules, CancellationToken cancellationToken) =>
        TypedResults.Ok(await schedules.ListAsync(projectKey, flagKey, environmentKey, cancellationToken));

    private static async Task<Ok<ScheduledChangeResponse>> GetAsync(
        string projectKey, string flagKey, string environmentKey, Guid changeId, ScheduleService schedules, CancellationToken cancellationToken) =>
        TypedResults.Ok(await schedules.GetAsync(projectKey, flagKey, environmentKey, changeId, cancellationToken));

    private static async Task<Created<ScheduledChangeResponse>> CreateAsync(
        string projectKey,
        string flagKey,
        string environmentKey,
        CreateScheduledChangeRequest request,
        ScheduleService schedules,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var change = await schedules.CreateAsync(projectKey, flagKey, environmentKey, request, user.ToActor(), cancellationToken);
        return TypedResults.Created(
            $"/api/v1/projects/{projectKey}/flags/{flagKey}/environments/{environmentKey}/scheduled-changes/{change.Id}", change);
    }

    private static async Task<Created<IReadOnlyList<ScheduledChangeResponse>>> CreateReleasePlanAsync(
        string projectKey,
        string flagKey,
        string environmentKey,
        CreateReleasePlanRequest request,
        ScheduleService schedules,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var changes = await schedules.CreateReleasePlanAsync(projectKey, flagKey, environmentKey, request, user.ToActor(), cancellationToken);
        return TypedResults.Created($"/api/v1/projects/{projectKey}/flags/{flagKey}/environments/{environmentKey}/scheduled-changes", changes);
    }

    private static async Task<NoContent> CancelAsync(
        string projectKey,
        string flagKey,
        string environmentKey,
        Guid changeId,
        ScheduleService schedules,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        await schedules.CancelAsync(projectKey, flagKey, environmentKey, changeId, user.ToActor(), cancellationToken);
        return TypedResults.NoContent();
    }
}
