using FlagForge.Application.Common;
using FlagForge.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FlagForge.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the SQL Server DbContext from <c>ConnectionStrings:Sql</c>, validated at startup.</summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Configure(options => options.ConnectionString = configuration.GetConnectionString("Sql") ?? string.Empty)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<FlagForgeDbContext>((provider, options) =>
            FlagForgeDbContext.Configure(options, provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));
        services.AddScoped<IFlagForgeDbContext>(provider => provider.GetRequiredService<FlagForgeDbContext>());
        return services;
    }
}
