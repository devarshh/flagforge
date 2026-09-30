using FlagForge.EvaluationApi.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FlagForge.EvaluationApi.Hubs;

/// <summary>
/// Each connection joins the group of its SDK key's environment. Every pod receives change messages from Redis and
/// notifies only its own connections, so no SignalR backplane is needed (ADR 0004).
/// </summary>
[Authorize(AuthenticationSchemes = SdkKeyAuthenticationHandler.SchemeName)]
public sealed class FlagsHub(HubConnectionTracker tracker, EvaluationMetrics metrics) : Hub<IFlagsClient>
{
    public const string Path = "/sdk/hubs/flags";

    public static string GroupName(Guid environmentId) => $"env:{environmentId}";

    public override async Task OnConnectedAsync()
    {
        var environmentId = Context.User!.GetEnvironmentId();
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(environmentId));
        tracker.Connected(environmentId);
        metrics.HubConnected();
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        tracker.Disconnected(Context.User!.GetEnvironmentId());
        metrics.HubDisconnected();
        await base.OnDisconnectedAsync(exception);
    }
}
