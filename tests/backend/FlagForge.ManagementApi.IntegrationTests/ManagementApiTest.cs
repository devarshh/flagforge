using FlagForge.Domain;
using FlagForge.Testing;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FlagForge.ManagementApi.IntegrationTests;

public abstract class ManagementApiTest(FlagForgeFixture fixture) : IntegrationTest(fixture)
{
    protected const string Development = "development";
    protected const string Staging = "staging";
    protected const string Production = "production";

    protected ManagementApiFactory Api => Fixture.ManagementApi;

    protected Task<ManagementApiDriver> AsAsync(Role role) => ManagementApiDriver.CreateAsync(Fixture, role, Ct);

    /// <summary>An anonymous HTTPS client that leaves cookies to the test.</summary>
    protected HttpClient AnonymousClient() =>
        Api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
}
