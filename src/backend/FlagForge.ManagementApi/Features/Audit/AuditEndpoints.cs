using FlagForge.Application.Audit;
using FlagForge.Application.Common;
using FlagForge.ManagementApi.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FlagForge.ManagementApi.Features.Audit;

internal static class AuditEndpoints
{
    public static RouteGroupBuilder MapAuditEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/audit", ListAsync)
            .RequireAuthorization(Policies.CanRead)
            .WithTags("Audit")
            .WithName("ListAuditEntries")
            .WithSummary("The audit log, newest first, filtered by project, flag, environment, actor, action, and time")
            .ProducesValidationProblem();
        return api;
    }

    private static async Task<Ok<PagedResult<AuditEntryResponse>>> ListAsync(
        [AsParameters] AuditQuery query, AuditService audit, CancellationToken cancellationToken) =>
        TypedResults.Ok(await audit.ListAsync(query, cancellationToken));
}
