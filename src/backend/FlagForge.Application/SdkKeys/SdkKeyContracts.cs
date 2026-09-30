using FlagForge.Domain;

namespace FlagForge.Application.SdkKeys;

/// <summary>An SDK key as listed: never the plaintext, only the display prefix.</summary>
public sealed record SdkKeyResponse(Guid Id, string Name, string KeyPrefix, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt)
{
    public static SdkKeyResponse From(SdkKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new SdkKeyResponse(key.Id, key.Name, key.KeyPrefix, key.CreatedAt, key.RevokedAt);
    }
}

public sealed record CreateSdkKeyRequest(string Name);

/// <summary>Returned once, at creation. FlagForge stores only a hash of <see cref="PlaintextKey"/>.</summary>
public sealed record CreatedSdkKeyResponse(Guid Id, string Name, string KeyPrefix, DateTimeOffset CreatedAt, string PlaintextKey);
