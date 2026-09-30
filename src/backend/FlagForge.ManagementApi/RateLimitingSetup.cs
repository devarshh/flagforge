using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FlagForge.ManagementApi;

internal sealed class ManagementRateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Sign-in attempts per client IP per minute (fixed window).</summary>
    [Range(1, 100_000)]
    public int LoginPermitsPerMinute { get; set; } = 10;
}

internal static class RateLimitingSetup
{
    public const string LoginPolicy = "login";

    public static IServiceCollection AddManagementRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ManagementRateLimitingOptions>()
            .Bind(configuration.GetSection(ManagementRateLimitingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Partitioned by client IP; forwarded headers make this the real client behind the gateway.
            limiter.AddPolicy(LoginPolicy, context =>
            {
                var permits = context.RequestServices.GetRequiredService<IOptions<ManagementRateLimitingOptions>>().Value.LoginPermitsPerMinute;
                return RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
            });

            limiter.OnRejected = async (context, cancellationToken) =>
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
                        Detail = "Too many sign-in attempts from this address. Wait a minute and try again.",
                    },
                });
            };
        });
        return services;
    }
}
