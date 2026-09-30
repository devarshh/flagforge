using FlagForge.Application.Common;
using FlagForge.Application.Schedules;
using FlagForge.Application.Stale;
using FlagForge.Infrastructure.Auditing;
using FlagForge.Infrastructure.Messaging;
using FlagForge.Infrastructure.Persistence;
using FlagForge.Infrastructure.Queries;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FlagForge.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the SQL Server DbContext from <c>ConnectionStrings:Sql</c> (validated at startup) and the
    /// persistence-backed implementations of the application's ports.
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Configure(options => options.ConnectionString = configuration.GetConnectionString("Sql") ?? string.Empty)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<FlagForgeDbContext>((provider, options) =>
            FlagForgeDbContext.Configure(options, provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));
        services.AddScoped<IFlagForgeDbContext>(provider => provider.GetRequiredService<FlagForgeDbContext>());
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IStaleFlagQuery, StaleFlagQuery>();
        services.AddScoped<IScheduledChangeClaimer, ScheduledChangeClaimer>();
        return services;
    }

    /// <summary>
    /// Registers the Redis connection used for change notifications. The app starts even when Redis is down:
    /// notifications are an accelerator, and snapshot TTLs keep evaluations correct without them.
    /// </summary>
    public static IServiceCollection AddRedisMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var options = ConfigurationOptions.Parse(provider.GetRequiredService<IOptions<RedisOptions>>().Value.ConnectionString);
            options.AbortOnConnectFail = false;
            options.ClientName ??= "flagforge";
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<IChangeNotifier, RedisChangeNotifier>();
        return services;
    }
}
