namespace FlagForge.Application.Common;

/// <summary>
/// Publishes change notifications after a transaction commits. Failures are logged, never thrown: snapshot TTLs heal
/// any missed message (see ADR 0003).
/// </summary>
public interface IChangeNotifier
{
    Task PublishConfigChangedAsync(IEnumerable<ConfigChangedMessage> changes, CancellationToken cancellationToken);

    Task PublishSdkKeyRevokedAsync(IEnumerable<Guid> sdkKeyIds, CancellationToken cancellationToken);
}
