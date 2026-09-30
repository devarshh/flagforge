namespace FlagForge.EvaluationApi.Authentication;

/// <summary>What an SDK key grants access to.</summary>
public sealed record SdkKeyIdentity(Guid SdkKeyId, Guid EnvironmentId, Guid ProjectId);
