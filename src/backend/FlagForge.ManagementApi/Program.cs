using FlagForge.Application;
using FlagForge.Infrastructure;
using FlagForge.ManagementApi;
using FlagForge.ManagementApi.Authentication;
using FlagForge.ManagementApi.Features.Audit;
using FlagForge.ManagementApi.Features.Auth;
using FlagForge.ManagementApi.Features.Environments;
using FlagForge.ManagementApi.Features.Flags;
using FlagForge.ManagementApi.Features.Meta;
using FlagForge.ManagementApi.Features.Projects;
using FlagForge.ManagementApi.Features.Schedules;
using FlagForge.ManagementApi.Features.SdkKeys;
using FlagForge.ManagementApi.Features.Stale;
using FlagForge.ManagementApi.Features.Targeting;
using FlagForge.ManagementApi.Features.Usage;
using FlagForge.ManagementApi.Features.Users;
using FlagForge.ServiceDefaults;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = 1024 * 1024);

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddRedisMessaging(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddManagementAuthentication(builder.Configuration);
builder.Services.AddManagementRateLimiting(builder.Configuration);
builder.Services.AddManagementOpenApi();
builder.Services.AddOptions<AppInfoOptions>().Bind(builder.Configuration);

var app = builder.Build();
app.UseServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapOpenApi();
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();

var api = app.MapGroup("/api/v1");
api.MapMetaEndpoints();
api.MapAuthEndpoints();
api.MapUserEndpoints();
api.MapProjectEndpoints();
api.MapEnvironmentEndpoints();
api.MapSdkKeyEndpoints();
api.MapFlagEndpoints();
api.MapTargetingEndpoints();
api.MapScheduleEndpoints();
api.MapUsageEndpoints();
api.MapStaleFlagEndpoints();
api.MapAuditEndpoints();

await app.RunAsync();
