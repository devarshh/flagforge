using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using FlagForge.Application.Common;
using FlagForge.EvaluationApi.Hubs;
using FlagForge.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using Testcontainers.Redis;
using Role = FlagForge.Domain.Role;

namespace FlagForge.EvaluationApi.IntegrationTests;

/// <summary>
/// Pods and Redis often start together, so an evaluation API may start before Redis accepts connections. It must still
/// receive changes once Redis is up, and drop what it cached while it could not hear about changes.
/// </summary>
public sealed class RedisOutageTests(FlagForgeFixture fixture) : IntegrationTest(fixture)
{
    private static readonly TimeSpan ReconnectTimeout = TimeSpan.FromSeconds(30);

    /// <summary>A config version no database has, so only the message handler can produce it.</summary>
    private const long MessageVersion = 999_999;

    [Fact]
    public async Task A_pod_that_starts_before_redis_catches_up_once_redis_is_reachable()
    {
        var admin = await ManagementApiDriver.CreateAsync(Fixture, Role.Admin, Ct);
        var project = (await admin.CreateProjectAsync(cancellationToken: Ct)).Key;
        var sdkKey = (await admin.CreateSdkKeyAsync(project, "development", Ct)).PlaintextKey;
        await using var db = Fixture.CreateDbContext();
        var environment = await db.Environments
            .Where(e => e.Project.Key == project && e.Key == "development")
            .Select(e => new { e.Id, e.ConfigVersion })
            .SingleAsync(Ct);

        // This test's own Redis, not started yet, on a fixed port so the API can reach it once it starts.
        var port = FreePort();
        await using var redis = new RedisBuilder(FlagForgeFixture.RedisImage).WithPortBinding(port, 6379).Build();
        await using var api = Fixture.EvaluationApi.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:ConnectionString"] = $"127.0.0.1:{port}",
            })));
        await using var connection = EvaluationApiFactory.CreateHubConnection(api.Server, sdkKey);
        var versions = new List<long>();
        connection.On<FlagsChangedMessage>(nameof(IFlagsClient.FlagsChanged), message =>
        {
            lock (versions)
            {
                versions.Add(message.EnvironmentVersion);
            }
        });
        await connection.StartAsync(Ct);

        await redis.StartAsync(Ct);

        // Connecting drops the caches and tells connected clients to refetch.
        await Eventually.AssertAsync(() => Received(environment.ConfigVersion), ReconnectTimeout, Ct, "the resynchronization notice");

        // The subscriptions made while Redis was down now deliver messages.
        await using var publisher = await ConnectionMultiplexer.ConnectAsync(redis.GetConnectionString());
        var channel = RedisChannel.Literal(Channels.ConfigChanged);
        var server = publisher.GetServer(publisher.GetEndPoints()[0]);
        await Eventually.AssertAsync(async () => await server.SubscriptionSubscriberCountAsync(channel) > 0, ReconnectTimeout, Ct, "the API to subscribe");
        await publisher.GetSubscriber().PublishAsync(channel, JsonSerializer.Serialize(new ConfigChangedMessage(environment.Id, MessageVersion), JsonDefaults.Options));
        await Eventually.AssertAsync(() => Received(MessageVersion), ReconnectTimeout, Ct, "FlagsChanged from the config-changed message");

        Task<bool> Received(long version)
        {
            lock (versions)
            {
                return Task.FromResult(versions.Contains(version));
            }
        }
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
