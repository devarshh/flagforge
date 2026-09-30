using System.Diagnostics.Metrics;

namespace FlagForge.EvaluationApi;

/// <summary>
/// The <c>FlagForge.Evaluation</c> meter. Evaluations are tagged only with <c>environment_id</c> to keep
/// cardinality bounded (no flag keys, no context keys).
/// </summary>
public sealed class EvaluationMetrics
{
    public const string MeterName = "FlagForge.Evaluation";

    private readonly Counter<long> _evaluations;
    private readonly Histogram<double> _snapshotLoadDuration;
    private readonly Counter<long> _configChangesReceived;
    private readonly UpDownCounter<long> _hubConnections;

    public EvaluationMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(MeterName);
        _evaluations = meter.CreateCounter<long>("flagforge.evaluations", "{evaluation}", "Flag results served to SDKs.");
        _snapshotLoadDuration = meter.CreateHistogram<double>("flagforge.snapshot.load.duration", "s", "Time to load and compile an environment snapshot.");
        _configChangesReceived = meter.CreateCounter<long>("flagforge.config_changes.received", "{message}", "Config-changed messages received from Redis.");
        _hubConnections = meter.CreateUpDownCounter<long>("flagforge.hub.connections", "{connection}", "Open SignalR connections.");
    }

    public void RecordEvaluations(Guid environmentId, int count) =>
        _evaluations.Add(count, new KeyValuePair<string, object?>("environment_id", environmentId.ToString()));

    public void RecordSnapshotLoad(TimeSpan duration) => _snapshotLoadDuration.Record(duration.TotalSeconds);

    public void RecordConfigChangeReceived() => _configChangesReceived.Add(1);

    public void HubConnected() => _hubConnections.Add(1);

    public void HubDisconnected() => _hubConnections.Add(-1);
}
