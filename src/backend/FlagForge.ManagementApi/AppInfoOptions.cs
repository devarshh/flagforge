namespace FlagForge.ManagementApi;

/// <summary>Build metadata set at image build time and shown in the dashboard footer.</summary>
internal sealed class AppInfoOptions
{
    [ConfigurationKeyName("APP_VERSION")]
    public string Version { get; set; } = "dev";

    [ConfigurationKeyName("APP_COMMIT")]
    public string Commit { get; set; } = "local";
}
