using FlagForge.Domain;
using FlagForge.Infrastructure;
using FlagForge.Migrator;
using FlagForge.Migrator.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Applies EF Core migrations, seeds the first administrator and (optionally) the demo project, then exits.
// Runs as a one-shot Compose service and as a Kubernetes Job; a non-zero exit code fails the deployment.
// Load appsettings from the app directory so "dotnet run --project" works from any folder, like the container does.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole();
}

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddOptions<SeedOptions>().Bind(builder.Configuration).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<DatabaseMigrator>();
builder.Services.AddScoped<AdminSeeder>();
builder.Services.AddScoped<DemoDataSeeder>();

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("FlagForge.Migrator");
var stopping = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
try
{
    // Starting the host runs options validation, so misconfiguration fails before touching the database.
    await host.StartAsync(stopping);
    await using (var scope = host.Services.CreateAsyncScope())
    {
        await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(stopping);
        await scope.ServiceProvider.GetRequiredService<AdminSeeder>().SeedAsync(stopping);
        await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(stopping);
    }

    MigratorLog.Finished(logger);
    await host.StopAsync(CancellationToken.None);
    return 0;
}
catch (Exception ex)
{
    MigratorLog.Failed(logger, ex);
    return 1;
}
