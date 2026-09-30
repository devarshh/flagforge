namespace FlagForge.Application.Common;

/// <summary>A compact reference to a user, for "changed by" style fields.</summary>
public sealed record UserRef(Guid Id, string DisplayName);
