using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace FlagForge.EvaluationApi.Snapshots;

/// <summary>
/// One snapshot per environment with single-flight loading: concurrent callers share one in-flight load. Entries
/// expire after the TTL and are evicted by change messages. Eviction removes the entry itself, so a load that was
/// already running when a change arrived is never cached; callers after the eviction start a fresh load.
/// </summary>
public sealed class SnapshotCache(IServiceScopeFactory scopes, TimeProvider timeProvider, IOptions<EvaluationOptions> options)
{
    private readonly ConcurrentDictionary<Guid, Lazy<Task<EnvironmentSnapshot?>>> _entries = new();

    private TimeSpan TimeToLive => TimeSpan.FromSeconds(options.Value.SnapshotTtlSeconds);

    /// <summary>The environment's snapshot, or null when the environment no longer exists.</summary>
    public async Task<EnvironmentSnapshot?> GetAsync(Guid environmentId, CancellationToken cancellationToken)
    {
        while (true)
        {
            var entry = _entries.GetOrAdd(environmentId, id => new Lazy<Task<EnvironmentSnapshot?>>(() => LoadAsync(id)));
            EnvironmentSnapshot? snapshot;
            try
            {
                snapshot = await entry.Value.WaitAsync(cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Failed loads are not cached; the next caller retries.
                _entries.TryRemove(KeyValuePair.Create(environmentId, entry));
                throw;
            }

            if (snapshot is not null && timeProvider.GetUtcNow() - snapshot.LoadedAt < TimeToLive)
            {
                return snapshot;
            }

            _entries.TryRemove(KeyValuePair.Create(environmentId, entry));
            if (snapshot is null)
            {
                return null;
            }
        }
    }

    public void Evict(Guid environmentId) => _entries.TryRemove(environmentId, out _);

    public void EvictAll() => _entries.Clear();

    private async Task<EnvironmentSnapshot?> LoadAsync(Guid environmentId)
    {
        // One load serves many callers, so no single caller's cancellation may cancel it.
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SnapshotLoader>().LoadAsync(environmentId, CancellationToken.None);
    }
}
