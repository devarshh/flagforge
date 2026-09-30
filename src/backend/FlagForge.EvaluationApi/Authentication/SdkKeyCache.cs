using System.Collections.Concurrent;

namespace FlagForge.EvaluationApi.Authentication;

/// <summary>
/// Valid SDK keys by hash, kept for 60 seconds. A revocation message evicts the key at once; the TTL bounds how long
/// a missed message can keep a revoked key working. Unknown keys are not cached.
/// </summary>
public sealed class SdkKeyCache(TimeProvider timeProvider)
{
    public static readonly TimeSpan TimeToLive = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, Entry> _byHash = new(StringComparer.Ordinal);

    public bool TryGet(string keyHash, out SdkKeyIdentity identity)
    {
        if (_byHash.TryGetValue(keyHash, out var entry))
        {
            if (entry.ExpiresAt > timeProvider.GetUtcNow())
            {
                identity = entry.Identity;
                return true;
            }

            _byHash.TryRemove(KeyValuePair.Create(keyHash, entry));
        }

        identity = null!;
        return false;
    }

    public void Set(string keyHash, SdkKeyIdentity identity) =>
        _byHash[keyHash] = new Entry(identity, timeProvider.GetUtcNow() + TimeToLive);

    public void Evict(Guid sdkKeyId)
    {
        foreach (var (hash, entry) in _byHash)
        {
            if (entry.Identity.SdkKeyId == sdkKeyId)
            {
                _byHash.TryRemove(KeyValuePair.Create(hash, entry));
            }
        }
    }

    public void Clear() => _byHash.Clear();

    private sealed record Entry(SdkKeyIdentity Identity, DateTimeOffset ExpiresAt);
}
