using FlagForge.Evaluation;

namespace FlagForge.EvaluationApi.Snapshots;

/// <summary>A flag in a snapshot: its id (for usage counts) and its compiled targeting.</summary>
public sealed record SnapshotFlag(Guid Id, CompiledFlag Compiled);

/// <summary>Every non-archived flag of one environment, compiled, plus the environment's <c>ConfigVersion</c>.</summary>
public sealed record EnvironmentSnapshot(
    Guid EnvironmentId,
    long ConfigVersion,
    IReadOnlyDictionary<string, SnapshotFlag> Flags,
    DateTimeOffset LoadedAt);
