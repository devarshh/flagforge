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
/// own connections in the environment's group; a revocation evicts the key. When the Redis subscription reconnects,
/// messages may have been missed, so all caches are dropped and connected environments are re-notified.
/// </summary>
public sealed partial class ChangeSubscriber(
    IConnectionMultiplexer redis,
    SnapshotCache snapshots,
    SdkKeyCache sdkKeys,
    HubConnectionTracker connections,
    IHubContext<FlagsHub, IFlagsClient> hub,
    EvaluationMetrics metrics,
    ILogger<ChangeSubscriber> logger) : IHostedService, IDisposable
{
    private readonly SemaphoreSlim _subscribeLock = new(1, 1);
    private ChannelMessageQueue? _configChanged;
    private ChannelMessageQueue? _sdkKeyRevoked;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        redis.ConnectionRestored += OnConnectionRestored;
        await EnsureSubscribedAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        redis.ConnectionRestored -= OnConnectionRestored;
        foreach (var queue in new[] { _configChanged, _sdkKeyRevoked })
        {
            if (queue is not null)
            {
                await queue.UnsubscribeAsync();
            }
        }
    }

    public void Dispose() => _subscribeLock.Dispose();

    private async Task HandleConfigChangedAsync(string payload)
    {
        metrics.RecordConfigChangeReceived();
        var change = JsonSerializer.Deserialize<ConfigChangedMessage>(payload, JsonDefaults.Options);
        if (change is null)
        {
            return;
        }

        snapshots.Evict(change.EnvironmentId);
        await hub.Clients.Group(FlagsHub.GroupName(change.EnvironmentId)).FlagsChanged(new FlagsChangedMessage(change.ConfigVersion));
    }

    private async Task EnsureSubscribedAsync()
    {
        await _subscribeLock.WaitAsync();
        try
        {
            var subscriber = redis.GetSubscriber();
            if (_configChanged is null)
            {
                _configChanged = await subscriber.SubscribeAsync(RedisChannel.Literal(Channels.ConfigChanged));
                _configChanged.OnMessage(message => RunSafelyAsync(() => HandleConfigChangedAsync(message.Message.ToString())));
            }

            if (_sdkKeyRevoked is null)
            {
                _sdkKeyRevoked = await subscriber.SubscribeAsync(RedisChannel.Literal(Channels.SdkKeyRevoked));
                _sdkKeyRevoked.OnMessage(message => RunSafelyAsync(() =>
                {
                    var revoked = JsonSerializer.Deserialize<SdkKeyRevokedMessage>(message.Message.ToString(), JsonDefaults.Options);
                    if (revoked is not null)
                    {
                        sdkKeys.Evict(revoked.SdkKeyId);
                    }

                    return Task.CompletedTask;
                }));
            }
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            // Evaluations stay correct without Redis: snapshots expire on their TTL. The reconnect handler retries.
            LogSubscribeFailed(logger, ex);
        }
        finally
        {
            _subscribeLock.Release();
        }
    }

    private void OnConnectionRestored(object? sender, ConnectionFailedEventArgs args)
    {
        if (args.ConnectionType == ConnectionType.Subscription)
        {
            _ = RunSafelyAsync(ResynchronizeAsync);
        }
    }

    private async Task ResynchronizeAsync()
    {
        LogResynchronizing(logger);
        snapshots.EvictAll();
        sdkKeys.Clear();
        await EnsureSubscribedAsync();
        foreach (var environmentId in connections.ConnectedEnvironments)
        {
            var snapshot = await snapshots.GetAsync(environmentId, CancellationToken.None);
            if (snapshot is not null)
            {
                await hub.Clients.Group(FlagsHub.GroupName(environmentId)).FlagsChanged(new FlagsChangedMessage(snapshot.ConfigVersion));
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
            LogHandlerFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not subscribe to Redis change channels; will retry when Redis reconnects")]
    private static partial void LogSubscribeFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Redis subscription restored; dropping cached snapshots and SDK keys and re-notifying clients")]
    private static partial void LogResynchronizing(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Handling a Redis change message failed")]
    private static partial void LogHandlerFailed(ILogger logger, Exception exception);
}
