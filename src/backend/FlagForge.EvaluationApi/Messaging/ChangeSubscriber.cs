using System.Text.Json;
using FlagForge.Application.Common;
using FlagForge.EvaluationApi.Authentication;
using FlagForge.EvaluationApi.Hubs;
using FlagForge.EvaluationApi.Snapshots;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace FlagForge.EvaluationApi.Messaging;

/// <summary>
/// Every pod subscribes independently. A config-changed message evicts that environment's snapshot and tells the pod's
/// own connections in the environment's group; a revocation evicts the key.
/// </summary>
/// <remarks>
/// The handlers are registered with the multiplexer even when Redis is unreachable at startup (pods and Redis often
/// start together), and the multiplexer subscribes them whenever a connection is established. Messages may have been
/// missed while disconnected, so every restored connection drops all caches and re-notifies connected environments.
/// That includes restored interactive connections: with RESP3, subscriptions share the interactive connection.
/// </remarks>
public sealed partial class ChangeSubscriber : IHostedService
{
    private static readonly RedisChannel ConfigChanged = RedisChannel.Literal(Channels.ConfigChanged);
    private static readonly RedisChannel SdkKeyRevoked = RedisChannel.Literal(Channels.SdkKeyRevoked);

    private readonly IConnectionMultiplexer _redis;
    private readonly SnapshotCache _snapshots;
    private readonly SdkKeyCache _sdkKeys;
    private readonly HubConnectionTracker _connections;
    private readonly IHubContext<FlagsHub, IFlagsClient> _hub;
    private readonly EvaluationMetrics _metrics;
    private readonly ILogger<ChangeSubscriber> _logger;
    private readonly Action<RedisChannel, RedisValue> _onConfigChanged;
    private readonly Action<RedisChannel, RedisValue> _onSdkKeyRevoked;

    public ChangeSubscriber(
        IConnectionMultiplexer redis,
        SnapshotCache snapshots,
        SdkKeyCache sdkKeys,
        HubConnectionTracker connections,
        IHubContext<FlagsHub, IFlagsClient> hub,
        EvaluationMetrics metrics,
        ILogger<ChangeSubscriber> logger)
    {
        _redis = redis;
        _snapshots = snapshots;
        _sdkKeys = sdkKeys;
        _connections = connections;
        _hub = hub;
        _metrics = metrics;
        _logger = logger;
        _onConfigChanged = (channel, payload) => _ = RunSafelyAsync(() => HandleConfigChangedAsync(payload.ToString()));
        _onSdkKeyRevoked = (channel, payload) => _ = RunSafelyAsync(() =>
        {
            HandleSdkKeyRevoked(payload.ToString());
            return Task.CompletedTask;
        });
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _redis.ConnectionRestored += OnConnectionRestored;
        var subscriber = _redis.GetSubscriber();
        await SubscribeAsync(subscriber, ConfigChanged, _onConfigChanged);
        await SubscribeAsync(subscriber, SdkKeyRevoked, _onSdkKeyRevoked);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _redis.ConnectionRestored -= OnConnectionRestored;
        var subscriber = _redis.GetSubscriber();
        try
        {
            await subscriber.UnsubscribeAsync(ConfigChanged, _onConfigChanged, CommandFlags.FireAndForget);
            await subscriber.UnsubscribeAsync(SdkKeyRevoked, _onSdkKeyRevoked, CommandFlags.FireAndForget);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            // Shutting down without Redis: the connection closes with the process anyway.
            LogUnsubscribeFailed(_logger, ex);
        }
    }

    private async Task SubscribeAsync(ISubscriber subscriber, RedisChannel channel, Action<RedisChannel, RedisValue> handler)
    {
        try
        {
            await subscriber.SubscribeAsync(channel, handler);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            // The multiplexer keeps the handler and subscribes it once Redis is reachable; until then, snapshots
            // expire on their TTL, so evaluations stay correct.
            LogSubscribeFailed(_logger, channel.ToString(), ex);
        }
    }

    private async Task HandleConfigChangedAsync(string payload)
    {
        _metrics.RecordConfigChangeReceived();
        var change = JsonSerializer.Deserialize<ConfigChangedMessage>(payload, JsonDefaults.Options);
        if (change is null)
        {
            return;
        }

        _snapshots.Evict(change.EnvironmentId);
        await _hub.Clients.Group(FlagsHub.GroupName(change.EnvironmentId)).FlagsChanged(new FlagsChangedMessage(change.ConfigVersion));
    }

    private void HandleSdkKeyRevoked(string payload)
    {
        var revoked = JsonSerializer.Deserialize<SdkKeyRevokedMessage>(payload, JsonDefaults.Options);
        if (revoked is not null)
        {
            _sdkKeys.Evict(revoked.SdkKeyId);
        }
    }

    private void OnConnectionRestored(object? sender, ConnectionFailedEventArgs args) =>
        _ = RunSafelyAsync(() => ResynchronizeAsync(args.ConnectionType));

    private async Task ResynchronizeAsync(ConnectionType connectionType)
    {
        LogResynchronizing(_logger, connectionType);
        _snapshots.EvictAll();
        _sdkKeys.Clear();
        foreach (var environmentId in _connections.ConnectedEnvironments)
        {
            var snapshot = await _snapshots.GetAsync(environmentId, CancellationToken.None);
            if (snapshot is not null)
            {
                await _hub.Clients.Group(FlagsHub.GroupName(environmentId)).FlagsChanged(new FlagsChangedMessage(snapshot.ConfigVersion));
            }
        }
    }

    private async Task RunSafelyAsync(Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A bad message or a transient failure must not stop the subscription; TTLs cover anything missed.
            LogHandlerFailed(_logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not subscribe to Redis channel {Channel}; it is subscribed when Redis becomes reachable")]
    private static partial void LogSubscribeFailed(ILogger logger, string channel, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Could not unsubscribe from Redis while stopping")]
    private static partial void LogUnsubscribeFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Redis connection ({ConnectionType}) established; dropping cached snapshots and SDK keys and re-notifying clients")]
    private static partial void LogResynchronizing(ILogger logger, ConnectionType connectionType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Handling a Redis change message failed")]
    private static partial void LogHandlerFailed(ILogger logger, Exception exception);
}
