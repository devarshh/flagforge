using System.Text.Json;
using FlagForge.Application.Flags;
using FlagForge.Application.Projects;
using FlagForge.Application.SdkKeys;
using FlagForge.Application.Targeting;
using FlagForge.Domain;
using FlagForge.Evaluation;

namespace FlagForge.Testing;

/// <summary>
/// Builds test data through the real management API, so arranged data goes through the same validation, audit, and
/// notification paths as production.
/// </summary>
public sealed class ManagementApiDriver(HttpClient client, User user)
{
    public HttpClient Client { get; } = client;

    public User User { get; } = user;

    public static async Task<ManagementApiDriver> CreateAsync(FlagForgeFixture fixture, Role role, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        var user = await fixture.CreateAsync(role, cancellationToken: cancellationToken);
        return new ManagementApiDriver(fixture.ManagementApi.CreateClientFor(user), user);
    }

    public static string UniqueKey(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 13, 64)];

    public async Task<ProjectResponse> CreateProjectAsync(string? key = null, CancellationToken cancellationToken = default)
    {
        var response = await Client.PostJsonAsync("/api/v1/projects", new CreateProjectRequest(key ?? UniqueKey("project"), "Test project"), cancellationToken);
        return await response.ReadJsonAsync<ProjectResponse>(cancellationToken);
    }

    public async Task<FlagResponse> CreateFlagAsync(
        string projectKey,
        string? key = null,
        FlagType type = FlagType.Boolean,
        IReadOnlyList<VariationInput>? variations = null,
        IReadOnlyList<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var request = new CreateFlagRequest(key ?? UniqueKey("flag"), "Test flag", type, Variations: variations, Tags: tags);
        var response = await Client.PostJsonAsync($"/api/v1/projects/{projectKey}/flags", request, cancellationToken);
        return await response.ReadJsonAsync<FlagResponse>(cancellationToken);
    }

    public static IReadOnlyList<VariationInput> StringVariations(params string[] values) =>
        [.. values.Select(v => new VariationInput(v, JsonSerializer.SerializeToElement(v)))];

    public async Task<TargetingResponse> GetTargetingAsync(string projectKey, string flagKey, string environmentKey, CancellationToken cancellationToken = default)
    {
        var response = await Client.GetAsync(TargetingUrl(projectKey, flagKey, environmentKey), cancellationToken);
        return await response.ReadJsonAsync<TargetingResponse>(cancellationToken);
    }

    /// <summary>Replaces the targeting, using the current version unless <paramref name="expectedVersion"/> is given.</summary>
    public async Task<TargetingResponse> UpdateTargetingAsync(
        string projectKey,
        string flagKey,
        string environmentKey,
        TargetingConfig config,
        string? comment = null,
        int? expectedVersion = null,
        CancellationToken cancellationToken = default)
    {
        var version = expectedVersion ?? (await GetTargetingAsync(projectKey, flagKey, environmentKey, cancellationToken)).Version;
        var response = await Client.PutJsonAsync(
            TargetingUrl(projectKey, flagKey, environmentKey), new UpdateTargetingRequest(config, version, comment), cancellationToken);
        return await response.ReadJsonAsync<TargetingResponse>(cancellationToken);
    }

    public async Task<TargetingResponse> ToggleAsync(
        string projectKey, string flagKey, string environmentKey, bool enabled, string? comment = null, CancellationToken cancellationToken = default)
    {
        var response = await Client.PostJsonAsync($"{TargetingUrl(projectKey, flagKey, environmentKey)}/toggle", new ToggleRequest(enabled, Comment: comment), cancellationToken);
        return await response.ReadJsonAsync<TargetingResponse>(cancellationToken);
    }

    public async Task<CreatedSdkKeyResponse> CreateSdkKeyAsync(string projectKey, string environmentKey, CancellationToken cancellationToken = default)
    {
        var response = await Client.PostJsonAsync(
            $"/api/v1/projects/{projectKey}/environments/{environmentKey}/sdk-keys", new CreateSdkKeyRequest("Test app"), cancellationToken);
        return await response.ReadJsonAsync<CreatedSdkKeyResponse>(cancellationToken);
    }

    public async Task<FlagResponse> ArchiveFlagAsync(string projectKey, string flagKey, CancellationToken cancellationToken = default)
    {
        var response = await Client.PostAsync($"/api/v1/projects/{projectKey}/flags/{flagKey}/archive", null, cancellationToken);
        return await response.ReadJsonAsync<FlagResponse>(cancellationToken);
    }

    public static string TargetingUrl(string projectKey, string flagKey, string environmentKey) =>
        $"/api/v1/projects/{projectKey}/flags/{flagKey}/environments/{environmentKey}";
}
