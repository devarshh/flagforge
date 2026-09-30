using System.Security.Claims;
using FlagForge.Application.Common;
using FlagForge.Application.Users;
using FlagForge.ManagementApi.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FlagForge.ManagementApi.Features.Users;

internal static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder api)
    {
        var users = api.MapGroup("/users")
            .WithTags("Users")
            .RequireAuthorization(Policies.CanAdmin)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
        users.MapGet("/", ListAsync).WithName("ListUsers").WithSummary("List users (paged)");
        users.MapGet("/{userId:guid}", GetAsync).WithName("GetUser").WithSummary("Get a user").ProducesProblem(StatusCodes.Status404NotFound);
        users.MapPost("/", CreateAsync)
            .WithName("CreateUser")
            .WithSummary("Create a user; the temporary password is returned once")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
        users.MapPatch("/{userId:guid}", UpdateAsync)
            .WithName("UpdateUser")
            .WithSummary("Change display name, role, or active state")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        users.MapPost("/{userId:guid}/reset-password", ResetPasswordAsync)
            .WithName("ResetUserPassword")
            .WithSummary("Issue a new temporary password; the user must change it at next sign-in")
            .ProducesProblem(StatusCodes.Status404NotFound);
        return api;
    }

    private static async Task<Ok<PagedResult<UserResponse>>> ListAsync([AsParameters] UserListQuery query, UserService users, CancellationToken cancellationToken) =>
        TypedResults.Ok(await users.ListAsync(query, cancellationToken));

    private static async Task<Ok<UserResponse>> GetAsync(Guid userId, UserService users, CancellationToken cancellationToken) =>
        TypedResults.Ok(await users.GetAsync(userId, cancellationToken));

    private static async Task<Created<CreateUserResponse>> CreateAsync(
        CreateUserRequest request, UserService users, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var created = await users.CreateAsync(request, user.ToActor(), cancellationToken);
        return TypedResults.Created($"/api/v1/users/{created.User.Id}", created);
    }

    private static async Task<Ok<UserResponse>> UpdateAsync(
        Guid userId, UpdateUserRequest request, UserService users, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        TypedResults.Ok(await users.UpdateAsync(userId, request, user.ToActor(), cancellationToken));

    private static async Task<Ok<ResetPasswordResponse>> ResetPasswordAsync(
        Guid userId, UserService users, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        TypedResults.Ok(await users.ResetPasswordAsync(userId, user.ToActor(), cancellationToken));
}
