using FlagForge.Domain;

namespace FlagForge.Application.Usage;

/// <summary>Adds hourly evaluation counts to <c>FlagUsageHourly</c>, creating rows as needed.</summary>
public interface IUsageStore
{
    /// <summary>Upserts every row atomically: either all counts are added or none are.</summary>
    Task AddCountsAsync(IReadOnlyCollection<FlagUsageHourly> rows, CancellationToken cancellationToken);
}
