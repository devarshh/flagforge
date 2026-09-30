using FlagForge.Worker.Jobs;

namespace FlagForge.Worker;

internal static class WorkerSetup
{
    public static IServiceCollection AddWorkerJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WorkerOptions>().Bind(configuration.GetSection(WorkerOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<RetentionOptions>().Bind(configuration.GetSection(RetentionOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<ScheduledChangeProcessor>();
        services.AddScoped<RetentionCleanup>();
        services.AddHostedService<ScheduledChangeJob>();
        services.AddHostedService<RetentionJob>();
        return services;
    }
}
