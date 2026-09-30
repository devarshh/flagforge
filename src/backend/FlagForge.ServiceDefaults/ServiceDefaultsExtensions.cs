using System.Diagnostics;
using FlagForge.Application.Common;
using FlagForge.ServiceDefaults.HealthChecks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FlagForge.ServiceDefaults;

/// <summary>
/// Cross-cutting defaults shared by the management API, evaluation API, and worker. Apps that call
/// <see cref="AddServiceDefaults"/> must also register persistence and Redis messaging, which the readiness checks use.
/// </summary>
public static class ServiceDefaultsExtensions
{
    public const string LiveTag = "live";
    public const string ReadyTag = "ready";

    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.ConfigureLogging();
        builder.ConfigureOpenTelemetry();

        builder.Services.AddSingleton<DatabaseHealthCache>();
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: [LiveTag])
            .AddCheck<DatabaseHealthCheck>("sql", tags: [ReadyTag], timeout: TimeSpan.FromSeconds(5))
            .AddCheck<RedisHealthCheck>("redis", failureStatus: HealthStatus.Degraded, tags: [ReadyTag], timeout: TimeSpan.FromSeconds(3));

        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
            context.ProblemDetails.Extensions.TryAdd("traceId", Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier);
        });
        builder.Services.AddExceptionHandler<AppExceptionHandler>();

        // Malformed JSON bodies surface as exceptions so AppExceptionHandler can report the offending field.
        builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        builder.Services.ConfigureHttpJsonOptions(options => JsonDefaults.Configure(options.SerializerOptions));

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;

            // The services are reachable only through the gateway (Compose network or ClusterIP), whose address is not
            // fixed, so every proxy is trusted; ForwardLimit = 1 still takes only the hop the gateway appended.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });
        return builder;
    }

    /// <summary>Middleware that must run first: forwarded headers, then exception and status-code ProblemDetails.</summary>
    public static WebApplication UseServiceDefaults(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }

    /// <summary>
    /// <c>/health/live</c> checks only the process; <c>/health/ready</c> checks SQL (and pending migrations) and Redis.
    /// Neither is routed through the gateway.
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Tags.Contains(LiveTag) });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) });
        return app;
    }

    private static void ConfigureLogging(this IHostApplicationBuilder builder)
    {
        builder.Logging.Configure(options => options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);
        if (!builder.Environment.IsDevelopment())
        {
            builder.Logging.ClearProviders();
            builder.Logging.AddJsonConsole(options =>
            {
                options.IncludeScopes = true;
                options.UseUtcTimestamp = true;
                options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z' ";
            });
        }
    }

    private static void ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? builder.Environment.ApplicationName;
        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName, serviceVersion: builder.Configuration["APP_VERSION"]))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options => options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation()
                .AddSource("FlagForge.*"))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("FlagForge.*"))
            .WithLogging();

        // Exporters are off unless an OTLP endpoint is configured (for example the Aspire dashboard profile).
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetry.UseOtlpExporter();
        }
    }
}
