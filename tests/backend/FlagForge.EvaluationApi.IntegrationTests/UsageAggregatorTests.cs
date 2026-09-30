using FlagForge.Application.Usage;
using FlagForge.Domain;
using FlagForge.EvaluationApi.Usage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FlagForge.EvaluationApi.IntegrationTests;

/// <summary>In-memory behaviour of usage counting; no containers needed.</summary>
public sealed class UsageAggregatorTests
{
    private static readonly Guid Environment = Guid.NewGuid();
    private static readonly Guid Flag = Guid.NewGuid();

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 30, 10, 15, 0, TimeSpan.Zero));

    [Fact]
    public void Counts_are_grouped_by_hour_and_drained_once()
    {
        var aggregator = new UsageAggregator(_time);
        aggregator.Record(Environment, Flag, "true");
        aggregator.Record(Environment, Flag, "true");
        aggregator.Record(Environment, Flag, "false");
        aggregator.Record(Environment, Flag, null);
        _time.Advance(TimeSpan.FromHours(1));
        aggregator.Record(Environment, Flag, "true");

        var rows = aggregator.Drain();

        rows.Select(r => (r.VariationId, r.HourStart.Hour, r.Count)).ShouldBe([("true", 10, 2L), ("false", 10, 1L), ("true", 11, 1L)], ignoreOrder: true);
        aggregator.Drain().ShouldBeEmpty();
    }

    [Fact]
    public void Restored_counts_merge_with_new_ones()
    {
        var aggregator = new UsageAggregator(_time);
        aggregator.Record(Environment, Flag, "true");
        var drained = aggregator.Drain();
        aggregator.Record(Environment, Flag, "true");

        aggregator.Restore(drained);

        aggregator.Drain().ShouldHaveSingleItem().Count.ShouldBe(2);
    }

    [Fact]
    public void Trimming_drops_the_oldest_hours_first()
    {
        var aggregator = new UsageAggregator(_time);
        for (var hour = 0; hour < 3; hour++)
        {
            aggregator.Record(Environment, Flag, "a");
            aggregator.Record(Environment, Flag, "b");
            _time.Advance(TimeSpan.FromHours(1));
        }

        var dropped = aggregator.TrimOldestHours(maxKeys: 4);

        dropped.ShouldBe(2);
        aggregator.Drain().Select(r => r.HourStart.Hour).Distinct().ShouldBe([11, 12], ignoreOrder: true);
    }

    [Fact]
    public async Task A_failed_flush_keeps_the_counts_for_the_next_attempt()
    {
        var aggregator = new UsageAggregator(_time);
        aggregator.Record(Environment, Flag, "true");
        var store = Substitute.For<IUsageStore>();
        store.AddCountsAsync(Arg.Any<IReadOnlyCollection<FlagUsageHourly>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("database unavailable"));
        await using var services = new ServiceCollection().AddSingleton(store).BuildServiceProvider();
        var flush = new UsageFlushService(
            aggregator, services.GetRequiredService<IServiceScopeFactory>(), _time, Options.Create(new EvaluationOptions()), NullLogger<UsageFlushService>.Instance);

        var written = await flush.FlushAsync(TestContext.Current.CancellationToken);

        written.ShouldBe(0);
        aggregator.Drain().ShouldHaveSingleItem().Count.ShouldBe(1);
        await store.Received(1).AddCountsAsync(Arg.Any<IReadOnlyCollection<FlagUsageHourly>>(), Arg.Any<CancellationToken>());
    }
}
