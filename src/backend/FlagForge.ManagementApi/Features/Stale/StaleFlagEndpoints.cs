using FlagForge.Application.Stale;
using FlagForge.ManagementApi.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FlagForge.ManagementApi.Features.Stale;

internal static class StaleFlagEndpoints
{
    public static RouteGroupBuilder MapStaleFlagEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectKey}/stale-flags", ListAsync)
            .RequireAuthorization(Policies.CanRead)
            .WithTags("Insights")
            .WithName("ListStaleFlags")
            .WithSummary("Flags that are no longer evaluated or serve a single variation everywhere")
            .ProducesProblem(StatusCodes.Status404NotFound);
        return api;
    }

    private static async Task<Ok<IReadOnlyList<StaleFlagResponse>>> ListAsync(
        string projectKey, StaleFlagService staleFlags, CancellationToken cancellationToken) =>
        TypedResults.Ok(await staleFlags.ListAsync(projectKey, cancellationToken));
}
