using System.Net.Http.Headers;
using FlagForge.Application.Common;
using FlagForge.Domain;
using FlagForge.ManagementApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlagForge.Testing;

/// <summary>The management API wired to the test containers, a fixed JWT key, and the fixture's fake clock.</summary>
public sealed class ManagementApiFactory(FlagForgeFixture fixture) : WebApplicationFactory<ManagementApiMarker>
{
    /// <summary>An HTTPS client with cookie handling, so the Secure, path-scoped refresh cookie round-trips.</summary>
    public HttpClient CreateBrowserClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });

    /// <summary>A client authenticated as <paramref name="user"/> with a freshly issued access token.</summary>
    public HttpClient CreateClientFor(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var client = CreateBrowserClient();
        var token = Services.GetRequiredService<IAccessTokenIssuer>().Issue(user);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Sql"] = fixture.SqlConnectionString,
            ["Redis:ConnectionString"] = fixture.RedisConnectionString,
            ["Auth:JwtSigningKey"] = TestAuth.SigningKey,
            ["RateLimiting:LoginPermitsPerMinute"] = "100000",
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(fixture.Time);
        });
    }
}
