using System.Security.Claims;
using FlagForge.Application.Common;
using FlagForge.Application.Flags;
using FlagForge.ManagementApi.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace FlagForge.ManagementApi.Features.Flags;

internal static class FlagEndpoints
{
    public static RouteGroupBuilder MapFlagEndpoints(this RouteGroupBuilder api)
    {
        var flags = api.MapGroup("/projects/{projectKey}/flags")
            .WithTags("Flags")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        flags.MapGet("/", ListAsync)
            .RequireAuthorization(Policies.CanRead)
            .WithName("ListFlags")
            .WithSummary("List flags with per-environment state, filtered by search, tag, and archived")
            .ProducesValidationProblem();
        flags.MapPost("/", CreateAsync)
            .RequireAuthorization(Policies.CanEdit)
            .WithName("CreateFlag")
            .WithSummary("Create a flag; every environment gets a default (off) config")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
        flags.MapGet("/{flagKey}", GetAsync).RequireAuthorization(Policies.CanRead).WithName("GetFlag").WithSummary("Get a flag with every environment's config");
        flags.MapPatch("/{flagKey}", UpdateAsync)
            .RequireAuthorization(Policies.CanEdit)
            .WithName("UpdateFlag")
            .WithSummary("Change name, description, tags, or the permanent setting")
            .ProducesValidationProblem();
        flags.MapPut("/{flagKey}/variations", ReplaceVariationsAsync)
            .RequireAuthorization(Policies.CanEdit)
            .WithName("ReplaceFlagVariations")
            .WithSummary("Replace the variations of a non-boolean flag")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
        flags.MapPost("/{flagKey}/archive", ArchiveAsync)
            .RequireAuthorization(Policies.CanEdit)
            .WithName("ArchiveFlag")
            .WithSummary("Archive a flag; SDKs stop receiving it and pending schedules are cancelled");
        flags.MapPost("/{flagKey}/restore", RestoreAsync).RequireAuthorization(Policies.CanEdit).WithName("RestoreFlag").WithSummary("Restore an archived flag");
        flags.MapDelete("/{flagKey}", DeleteAsync)
            .RequireAuthorization(Policies.CanAdmin)
            .WithName("DeleteFlag")
            .WithSummary("Permanently delete an archived flag; confirmKey must equal the flag key")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
        return api;
    }

    private static async Task<Ok<PagedResult<FlagSummaryResponse>>> ListAsync(
        string projectKey, [AsParameters] FlagListQuery query, FlagService flags, CancellationToken cancellationToken) =>
        TypedResults.Ok(await flags.ListAsync(projectKey, query, cancellationToken));

    private static async Task<Created<FlagResponse>> CreateAsync(
        string projectKey, CreateFlagRequest request, FlagService flags, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var flag = await flags.CreateAsync(projectKey, request, user.ToActor(), cancellationToken);
        return TypedResults.Created($"/api/v1/projects/{projectKey}/flags/{flag.Key}", flag);
    }

    private static async Task<Ok<FlagResponse>> GetAsync(string projectKey, string flagKey, FlagService flags, CancellationToken cancellationToken) =>
        TypedResults.Ok(await flags.GetAsync(projectKey, flagKey, cancellationToken));

    private static async Task<Ok<FlagResponse>> UpdateAsync(
        string projectKey, string flagKey, UpdateFlagRequest request, FlagService flags, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        TypedResults.Ok(await flags.UpdateAsync(projectKey, flagKey, request, user.ToActor(), cancellationToken));

    private static async Task<Ok<FlagResponse>> ReplaceVariationsAsync(
        string projectKey, string flagKey, ReplaceVariationsRequest request, FlagService flags, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        TypedResults.Ok(await flags.ReplaceVariationsAsync(projectKey, flagKey, request, user.ToActor(), cancellationToken));

    private static async Task<Ok<FlagResponse>> ArchiveAsync(
        string projectKey, string flagKey, FlagService flags, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        TypedResults.Ok(await flags.ArchiveAsync(projectKey, flagKey, user.ToActor(), cancellationToken));

    private static async Task<Ok<FlagResponse>> RestoreAsync(
        string projectKey, string flagKey, FlagService flags, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        TypedResults.Ok(await flags.RestoreAsync(projectKey, flagKey, user.ToActor(), cancellationToken));

    private static async Task<NoContent> DeleteAsync(
        string projectKey, string flagKey, [FromBody] ConfirmKeyRequest request, FlagService flags, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        await flags.DeleteAsync(projectKey, flagKey, request, user.ToActor(), cancellationToken);
        return TypedResults.NoContent();
    }
}
