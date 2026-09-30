using System.Security.Claims;
using FlagForge.Application.Common;
using FlagForge.Application.Environments;
using FlagForge.ManagementApi.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace FlagForge.ManagementApi.Features.Environments;

internal static class EnvironmentEndpoints
{
    public static RouteGroupBuilder MapEnvironmentEndpoints(this RouteGroupBuilder api)
    {
        var environments = api.MapGroup("/projects/{projectKey}/environments")
            .WithTags("Environments")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        environments.MapGet("/", ListAsync).RequireAuthorization(Policies.CanRead).WithName("ListEnvironments").WithSummary("List a project's environments");
        environments.MapGet("/{environmentKey}", GetAsync).RequireAuthorization(Policies.CanRead).WithName("GetEnvironment").WithSummary("Get an environment");
        environments.MapPost("/", CreateAsync)
            .RequireAuthorization(Policies.CanAdmin)
            .WithName("CreateEnvironment")
            .WithSummary("Create an environment; every flag gets a default config in it")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
        environments.MapPatch("/{environmentKey}", UpdateAsync)
            .RequireAuthorization(Policies.CanAdmin)
            .WithName("UpdateEnvironment")
            .WithSummary("Change name, color, protection, or sort order")
            .ProducesValidationProblem();
        environments.MapDelete("/{environmentKey}", DeleteAsync)
            .RequireAuthorization(Policies.CanAdmin)
            .WithName("DeleteEnvironment")
            .WithSummary("Delete an environment (not the last one); confirmKey must equal the environment key")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
        return api;
    }

    private static async Task<Ok<IReadOnlyList<EnvironmentResponse>>> ListAsync(
        string projectKey, EnvironmentService environments, CancellationToken cancellationToken) =>
        TypedResults.Ok(await environments.ListAsync(projectKey, cancellationToken));

    private static async Task<Ok<EnvironmentResponse>> GetAsync(
        string projectKey, string environmentKey, EnvironmentService environments, CancellationToken cancellationToken) =>
        TypedResults.Ok(await environments.GetAsync(projectKey, environmentKey, cancellationToken));

    private static async Task<Created<EnvironmentResponse>> CreateAsync(
        string projectKey, CreateEnvironmentRequest request, EnvironmentService environments, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var environment = await environments.CreateAsync(projectKey, request, user.ToActor(), cancellationToken);
        return TypedResults.Created($"/api/v1/projects/{projectKey}/environments/{environment.Key}", environment);
    }

    private static async Task<Ok<EnvironmentResponse>> UpdateAsync(
        string projectKey,
        string environmentKey,
        UpdateEnvironmentRequest request,
        EnvironmentService environments,
        ClaimsPrincipal user,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await environments.UpdateAsync(projectKey, environmentKey, request, user.ToActor(), cancellationToken));

    private static async Task<NoContent> DeleteAsync(
        string projectKey,
        string environmentKey,
        [FromBody] ConfirmKeyRequest request,
        EnvironmentService environments,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        await environments.DeleteAsync(projectKey, environmentKey, request, user.ToActor(), cancellationToken);
        return TypedResults.NoContent();
    }
}
