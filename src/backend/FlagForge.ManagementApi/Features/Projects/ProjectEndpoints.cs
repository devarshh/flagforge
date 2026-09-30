using System.Security.Claims;
using FlagForge.Application.Common;
using FlagForge.Application.Projects;
using FlagForge.ManagementApi.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace FlagForge.ManagementApi.Features.Projects;

internal static class ProjectEndpoints
{
    public static RouteGroupBuilder MapProjectEndpoints(this RouteGroupBuilder api)
    {
        var projects = api.MapGroup("/projects")
            .WithTags("Projects")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        projects.MapGet("/", ListAsync).RequireAuthorization(Policies.CanRead).WithName("ListProjects").WithSummary("List projects with their environments");
        projects.MapPost("/", CreateAsync)
            .RequireAuthorization(Policies.CanAdmin)
            .WithName("CreateProject")
            .WithSummary("Create a project with development, staging, and production (protected) environments")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
        projects.MapGet("/{projectKey}", GetAsync)
            .RequireAuthorization(Policies.CanRead)
            .WithName("GetProject")
            .WithSummary("Get a project")
            .ProducesProblem(StatusCodes.Status404NotFound);
        projects.MapPatch("/{projectKey}", UpdateAsync)
            .RequireAuthorization(Policies.CanAdmin)
            .WithName("UpdateProject")
            .WithSummary("Rename a project or change its description")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
        projects.MapDelete("/{projectKey}", DeleteAsync)
            .RequireAuthorization(Policies.CanAdmin)
            .WithName("DeleteProject")
            .WithSummary("Delete a project and everything in it; confirmKey must equal the project key")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
        return api;
    }

    private static async Task<Ok<IReadOnlyList<ProjectResponse>>> ListAsync(ProjectService projects, CancellationToken cancellationToken) =>
        TypedResults.Ok(await projects.ListAsync(cancellationToken));

    private static async Task<Created<ProjectResponse>> CreateAsync(
        CreateProjectRequest request, ProjectService projects, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var project = await projects.CreateAsync(request, user.ToActor(), cancellationToken);
        return TypedResults.Created($"/api/v1/projects/{project.Key}", project);
    }

    private static async Task<Ok<ProjectResponse>> GetAsync(string projectKey, ProjectService projects, CancellationToken cancellationToken) =>
        TypedResults.Ok(await projects.GetAsync(projectKey, cancellationToken));

    private static async Task<Ok<ProjectResponse>> UpdateAsync(
        string projectKey, UpdateProjectRequest request, ProjectService projects, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        TypedResults.Ok(await projects.UpdateAsync(projectKey, request, user.ToActor(), cancellationToken));

    private static async Task<NoContent> DeleteAsync(
        string projectKey, [FromBody] ConfirmKeyRequest request, ProjectService projects, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        await projects.DeleteAsync(projectKey, request, user.ToActor(), cancellationToken);
        return TypedResults.NoContent();
    }
}
