using System.Text.Json;
using FlagForge.Application.Common;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace FlagForge.Infrastructure.Messaging;

/// <summary>
/// Publishes change notifications to Redis. A failed publish is logged as a warning and swallowed on purpose: the
/// change is already committed, and evaluation snapshots expire on their own TTL (ADR 0003).
/// </summary>
internal sealed partial class RedisChangeNotifier(IConnectionMultiplexer redis, ILogger<RedisChangeNotifier> logger) : IChangeNotifier
{
    private static readonly RedisChannel ConfigChanged = RedisChannel.Literal(Channels.ConfigChanged);
    private static readonly RedisChannel SdkKeyRevoked = RedisChannel.Literal(Channels.SdkKeyRevoked);

    public async Task PublishConfigChangedAsync(IEnumerable<ConfigChangedMessage> changes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        foreach (var change in changes)
        {
            await PublishAsync(ConfigChanged, JsonSerializer.Serialize(change, JsonDefaults.Options));
        }
    }

    public async Task PublishSdkKeyRevokedAsync(IEnumerable<Guid> sdkKeyIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sdkKeyIds);
        foreach (var sdkKeyId in sdkKeyIds)
        {
            await PublishAsync(SdkKeyRevoked, JsonSerializer.Serialize(new SdkKeyRevokedMessage(sdkKeyId), JsonDefaults.Options));
        }
    }

    private async Task PublishAsync(RedisChannel channel, string payload)
    {
        try
        {
            await redis.GetSubscriber().PublishAsync(channel, payload);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            LogPublishFailed(logger, ex, channel.ToString());
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not publish to Redis channel {Channel}; subscribers will catch up when their snapshots expire")]
    private static partial void LogPublishFailed(ILogger logger, Exception exception, string channel);
}
