using Microsoft.OpenApi;

namespace FlagForge.ManagementApi;

internal static class OpenApiSetup
{
    public const string BearerScheme = "Bearer";

    public static IServiceCollection AddManagementOpenApi(this IServiceCollection services) =>
        services.AddOpenApi("v1", options => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info = new OpenApiInfo
            {
                Title = "FlagForge Management API",
                Version = "v1",
                Description = "Dashboard backend: projects, environments, flags, targeting, schedules, SDK keys, users, and the audit log. "
                    + "Sign in with POST /api/v1/auth/login and send the access token as a Bearer token.",
            };
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Access token from POST /api/v1/auth/login (valid for 15 minutes).",
            };
            document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(BearerScheme, document)] = [] }];
            return Task.CompletedTask;
        }));
}
