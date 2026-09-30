using FlagForge.Worker;
using FlagForge.Worker.Jobs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace FlagForge.Testing;

/// <summary>
/// The worker host wired to the test containers and the fake clock. Its periodic jobs are removed so tests drive
/// processing directly and deterministically; <see cref="WithJobs"/> returns a host that keeps them.
/// </summary>
public sealed class WorkerFactory(FlagForgeFixture fixture, bool keepJobs = false) : WebApplicationFactory<WorkerMarker>
{
    public WorkerFactory WithJobs() => new(fixture, keepJobs: true);

    /// <summary>A new processor instance, as a second worker replica would have.</summary>
    public ScheduledChangeProcessor CreateProcessor() => ActivatorUtilities.CreateInstance<ScheduledChangeProcessor>(Services);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Sql"] = fixture.SqlConnectionString,
            ["Redis:ConnectionString"] = fixture.RedisConnectionString,
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(fixture.Time);
            if (!keepJobs)
            {
                foreach (var job in services.Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType?.IsSubclassOf(typeof(PeriodicJob)) == true).ToList())
                {
                    services.Remove(job);
                }
            }
        });
    }
}
