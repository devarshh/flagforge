using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace FlagForge.ManagementApi.Features.Meta;

internal sealed record MetaResponse(string Version, string Commit);

internal static class MetaEndpoints
{
    public static RouteGroupBuilder MapMetaEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/meta", GetMeta)
            .AllowAnonymous()
            .WithTags("Meta")
            .WithName("GetMeta")
            .WithSummary("Build version and commit, shown in the dashboard footer");
        return api;
    }

    private static Ok<MetaResponse> GetMeta(IOptions<AppInfoOptions> appInfo) =>
        TypedResults.Ok(new MetaResponse(appInfo.Value.Version, appInfo.Value.Commit));
}
