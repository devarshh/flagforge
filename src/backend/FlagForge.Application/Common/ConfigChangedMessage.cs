namespace FlagForge.Application.Common;

/// <summary>Published after an environment's evaluation output changed; the version orders messages.</summary>
public sealed record ConfigChangedMessage(Guid EnvironmentId, long ConfigVersion);
