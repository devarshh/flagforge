using FlagForge.Application.Audit;
using FlagForge.Application.Auth;
using FlagForge.Application.Environments;
using FlagForge.Application.Flags;
using FlagForge.Application.Projects;
using FlagForge.Application.Schedules;
using FlagForge.Application.SdkKeys;
using FlagForge.Application.Stale;
using FlagForge.Application.Targeting;
using FlagForge.Application.Usage;
using FlagForge.Application.Users;
using FlagForge.Domain;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlagForge.Application;

public static class DependencyInjection
{
    /// <summary>Registers the use-case services and their request validators.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>(ServiceLifetime.Singleton);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<EnvironmentService>();
        services.AddScoped<SdkKeyService>();
        services.AddScoped<FlagService>();
        services.AddScoped<TargetingService>();
        services.AddScoped<ScheduleService>();
        services.AddScoped<UsageService>();
        services.AddScoped<StaleFlagService>();
        services.AddScoped<AuditService>();
        return services;
    }
}
