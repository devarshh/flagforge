using FlagForge.Application.Usage;
using FlagForge.ManagementApi.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FlagForge.ManagementApi.Features.Usage;

internal static class UsageEndpoints
{
    public static RouteGroupBuilder MapUsageEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectKey}/flags/{flagKey}/environments/{environmentKey}/usage", GetAsync)
            .RequireAuthorization(Policies.CanRead)
            .WithTags("Insights")
            .WithName("GetFlagUsage")
            .WithSummary("Evaluations per variation, bucketed by hour or day (at most 90 days)")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
        return api;
    }

    private static async Task<Ok<IReadOnlyList<UsageBucket>>> GetAsync(
        string projectKey,
        string flagKey,
        string environmentKey,
        [AsParameters] UsageQuery query,
        UsageService usage,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await usage.GetAsync(projectKey, flagKey, environmentKey, query, cancellationToken));
}
