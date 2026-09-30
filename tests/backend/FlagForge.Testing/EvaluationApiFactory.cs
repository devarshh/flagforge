using System.Net.Http.Headers;
using FlagForge.Application.Common;
using FlagForge.EvaluationApi;
using FlagForge.EvaluationApi.Authentication;
using FlagForge.EvaluationApi.Snapshots;
using FlagForge.EvaluationApi.Usage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlagForge.Testing;

/// <summary>The evaluation API wired to the test containers and the fixture's fake clock.</summary>
public sealed class EvaluationApiFactory(FlagForgeFixture fixture) : WebApplicationFactory<EvaluationApiMarker>
{
    public HttpClient CreateSdkClient(string sdkKey)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sdkKey);
        return client;
    }

    /// <summary>
    /// A hub connection that behaves like the browser SDK: WebSockets only, no negotiation, and the SDK key in the
    /// <c>access_token</c> query parameter.
    /// </summary>
    public HubConnection CreateHubConnection(string sdkKey) => CreateHubConnection(Server, sdkKey);

    public static HubConnection CreateHubConnection(TestServer server, string sdkKey)
    {
        ArgumentNullException.ThrowIfNull(server);
        return new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, $"/sdk/hubs/flags?access_token={Uri.EscapeDataString(sdkKey)}"), options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.WebSocketFactory = async (context, cancellationToken) =>
                    await server.CreateWebSocketClient().ConnectAsync(context.Uri, cancellationToken);
            })
            .AddJsonProtocol(options => JsonDefaults.Configure(options.PayloadSerializerOptions))
            .Build();
    }

    public UsageFlushService UsageFlush => Services.GetRequiredService<UsageFlushService>();

    internal void ResetState()
    {
        Services.GetRequiredService<SnapshotCache>().EvictAll();
        Services.GetRequiredService<SdkKeyCache>().Clear();
        Services.GetRequiredService<UsageAggregator>().Drain();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Sql"] = fixture.SqlConnectionString,
            ["Redis:ConnectionString"] = fixture.RedisConnectionString,
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(fixture.Time);
        });
    }
}
