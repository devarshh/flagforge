using System.Security.Claims;
using FlagForge.Application.SdkKeys;
using FlagForge.ManagementApi.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FlagForge.ManagementApi.Features.SdkKeys;

internal static class SdkKeyEndpoints
{
    public static RouteGroupBuilder MapSdkKeyEndpoints(this RouteGroupBuilder api)
    {
        var keys = api.MapGroup("/projects/{projectKey}/environments/{environmentKey}/sdk-keys")
            .WithTags("SDK keys")
            .RequireAuthorization(Policies.CanAdmin)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        keys.MapGet("/", ListAsync).WithName("ListSdkKeys").WithSummary("List SDK keys by prefix (never the plaintext)");
        keys.MapGet("/{keyId:guid}", GetAsync).WithName("GetSdkKey").WithSummary("Get an SDK key by id");
        keys.MapPost("/", CreateAsync)
            .WithName("CreateSdkKey")
            .WithSummary("Create an SDK key; the plaintext key is returned once")
            .ProducesValidationProblem();
        keys.MapDelete("/{keyId:guid}", RevokeAsync).WithName("RevokeSdkKey").WithSummary("Revoke an SDK key; it stops working within seconds");
        return api;
    }

    private static async Task<Ok<IReadOnlyList<SdkKeyResponse>>> ListAsync(
        string projectKey, string environmentKey, SdkKeyService keys, CancellationToken cancellationToken) =>
        TypedResults.Ok(await keys.ListAsync(projectKey, environmentKey, cancellationToken));

    private static async Task<Ok<SdkKeyResponse>> GetAsync(
        string projectKey, string environmentKey, Guid keyId, SdkKeyService keys, CancellationToken cancellationToken) =>
        TypedResults.Ok(await keys.GetAsync(projectKey, environmentKey, keyId, cancellationToken));

    private static async Task<Created<CreatedSdkKeyResponse>> CreateAsync(
        string projectKey, string environmentKey, CreateSdkKeyRequest request, SdkKeyService keys, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var key = await keys.CreateAsync(projectKey, environmentKey, request, user.ToActor(), cancellationToken);
        return TypedResults.Created($"/api/v1/projects/{projectKey}/environments/{environmentKey}/sdk-keys/{key.Id}", key);
    }

    private static async Task<NoContent> RevokeAsync(
        string projectKey, string environmentKey, Guid keyId, SdkKeyService keys, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        await keys.RevokeAsync(projectKey, environmentKey, keyId, user.ToActor(), cancellationToken);
        return TypedResults.NoContent();
    }
}
