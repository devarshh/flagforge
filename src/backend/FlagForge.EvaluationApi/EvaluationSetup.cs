using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using FlagForge.Application.Common;
using FlagForge.EvaluationApi.Authentication;
using FlagForge.EvaluationApi.Endpoints;
using FlagForge.EvaluationApi.Hubs;
using FlagForge.EvaluationApi.Messaging;
using FlagForge.EvaluationApi.Snapshots;
using FlagForge.EvaluationApi.Usage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FlagForge.EvaluationApi;

internal static class EvaluationSetup
{
    public static IServiceCollection AddEvaluationApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EvaluationOptions>().Bind(configuration.GetSection(EvaluationOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SdkRateLimitingOptions>().Bind(configuration.GetSection(SdkRateLimitingOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SdkCorsOptions>().Bind(configuration.GetSection(SdkCorsOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();

        services.AddSingleton<EvaluationMetrics>();
        services.AddSingleton<SdkKeyCache>();
        services.AddSingleton<SnapshotCache>();
        services.AddScoped<SnapshotLoader>();
        services.AddSingleton<HubConnectionTracker>();
        services.AddSingleton<UsageAggregator>();
        services.AddSingleton<UsageFlushService>();
        services.AddHostedService(provider => provider.GetRequiredService<UsageFlushService>());
        services.AddSingleton<ChangeSubscriber>();
        services.AddHostedService(provider => provider.GetRequiredService<ChangeSubscriber>());

        services.AddAuthentication(SdkKeyAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, SdkKeyAuthenticationHandler>(SdkKeyAuthenticationHandler.SchemeName, null);
        services.AddAuthorization();

        services.AddSignalR().AddJsonProtocol(options => JsonDefaults.Configure(options.PayloadSerializerOptions));
        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IOptions<SdkCorsOptions>>((cors, sdk) => cors.AddPolicy(SdkEndpoints.CorsPolicy, policy =>
        {
            var origins = sdk.Value.Origins;
            if (origins.Contains("*"))
            {
                // Any origin, but never with credentials: SDK keys travel in the Authorization header.
                policy.AllowAnyOrigin();
            }
            else
            {
                policy.WithOrigins([.. origins]);
            }

            policy.AllowAnyHeader().WithMethods("GET", "POST");
        }));

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.AddPolicy(SdkEndpoints.RateLimitPolicy, context =>
            {
                var limits = context.RequestServices.GetRequiredService<IOptions<SdkRateLimitingOptions>>().Value;
                return RateLimitPartition.GetTokenBucketLimiter(
                    context.User.FindFirstValue(SdkClaims.SdkKeyId) ?? "anonymous",
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = limits.SdkBurst,
                        TokensPerPeriod = limits.SdkPermitsPerSecond,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    });
            });
            limiter.OnRejected = async (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests.",
                        Detail = "This SDK key is over its request budget. Retry after the time in the Retry-After header.",
                    },
                });
            };
        });
        return services;
    }
}
