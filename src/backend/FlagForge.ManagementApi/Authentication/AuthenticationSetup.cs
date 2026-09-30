using FlagForge.Application.Common;
using FlagForge.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FlagForge.ManagementApi.Authentication;

internal static class AuthenticationSetup
{
    public static IServiceCollection AddManagementAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IAccessTokenIssuer, JwtTokenIssuer>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthOptions>, TimeProvider>((jwt, auth, timeProvider) =>
            {
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = auth.Value.JwtIssuer,
                    ValidAudience = auth.Value.JwtAudience,
                    IssuerSigningKey = JwtTokenIssuer.SigningKey(auth.Value),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = TokenClaims.Name,
                    RoleClaimType = TokenClaims.Role,

                    // Validate against the injected clock, the same one that issued the token.
                    LifetimeValidator = (notBefore, expires, _, parameters) =>
                    {
                        var now = timeProvider.GetUtcNow().UtcDateTime;
                        return (notBefore is null || notBefore.Value <= now + parameters.ClockSkew)
                            && expires is not null
                            && expires.Value >= now - parameters.ClockSkew;
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.CanRead, policy => policy.RequireRole(nameof(Role.Viewer), nameof(Role.Editor), nameof(Role.Admin)))
            .AddPolicy(Policies.CanEdit, policy => policy.RequireRole(nameof(Role.Editor), nameof(Role.Admin)))
            .AddPolicy(Policies.CanAdmin, policy => policy.RequireRole(nameof(Role.Admin)));
        return services;
    }
}
