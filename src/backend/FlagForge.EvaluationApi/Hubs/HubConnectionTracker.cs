using System.Collections.Concurrent;

namespace FlagForge.EvaluationApi.Hubs;

/// <summary>Counts this pod's hub connections per environment, so a Redis reconnect can re-notify exactly those groups.</summary>
public sealed class HubConnectionTracker
{
    private readonly ConcurrentDictionary<Guid, int> _connections = new();

    public IReadOnlyCollection<Guid> ConnectedEnvironments => [.. _connections.Where(c => c.Value > 0).Select(c => c.Key)];

    public void Connected(Guid environmentId) => _connections.AddOrUpdate(environmentId, 1, static (_, count) => count + 1);

    public void Disconnected(Guid environmentId)
    {
        var remaining = _connections.AddOrUpdate(environmentId, 0, static (_, count) => Math.Max(0, count - 1));
        if (remaining == 0)
        {
            _connections.TryRemove(KeyValuePair.Create(environmentId, 0));
        }
    }
}
