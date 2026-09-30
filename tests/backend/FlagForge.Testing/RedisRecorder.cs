using System.Collections.Concurrent;
using System.Text.Json;
using FlagForge.Application.Common;
using StackExchange.Redis;

namespace FlagForge.Testing;

public static class RedisRecorder
{
    /// <summary>Subscribes to <paramref name="channel"/> and records each message deserialized as <typeparamref name="T"/>.</summary>
    public static async Task<RedisRecorder<T>> StartAsync<T>(string connectionString, string channel)
    {
        var recorder = new RedisRecorder<T>(await ConnectionMultiplexer.ConnectAsync(connectionString));
        await recorder.SubscribeAsync(channel);
        return recorder;
    }
}

/// <summary>Records every message on a Redis channel, so tests can assert on what was published.</summary>
public sealed class RedisRecorder<T> : IAsyncDisposable
{
    private readonly ConnectionMultiplexer _connection;
    private readonly ConcurrentQueue<T> _messages = new();

    internal RedisRecorder(ConnectionMultiplexer connection) => _connection = connection;

    public IReadOnlyCollection<T> Messages => _messages;

    public Task<IReadOnlyCollection<T>> WaitForAsync(Func<T, bool> match, TimeSpan timeout, CancellationToken cancellationToken) =>
        Eventually.GetAsync(
            () => Task.FromResult<IReadOnlyCollection<T>>([.. _messages]),
            messages => messages.Any(match),
            timeout,
            cancellationToken,
            $"a matching {typeof(T).Name}");

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    internal Task SubscribeAsync(string channel) =>
        _connection.GetSubscriber().SubscribeAsync(RedisChannel.Literal(channel), (_, value) =>
        {
            var message = JsonSerializer.Deserialize<T>(value.ToString(), JsonDefaults.Options);
            if (message is not null)
            {
                _messages.Enqueue(message);
            }
        });
}
