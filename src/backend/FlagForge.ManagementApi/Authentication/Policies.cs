namespace FlagForge.ManagementApi.Authentication;

/// <summary>Authorization policies. Protected environments add an Admin-only check in the application services.</summary>
internal static class Policies
{
    /// <summary>Viewer, Editor, or Admin.</summary>
    public const string CanRead = nameof(CanRead);

    /// <summary>Editor or Admin.</summary>
    public const string CanEdit = nameof(CanEdit);

    /// <summary>Admin only.</summary>
    public const string CanAdmin = nameof(CanAdmin);
}
