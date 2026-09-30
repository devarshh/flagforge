using FlagForge.EvaluationApi;
using FlagForge.EvaluationApi.Endpoints;
using FlagForge.Infrastructure;
using FlagForge.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Hub requests carry the SDK key in the query string, and request-start logs print full URLs.
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = 32 * 1024);

// Leaves room for the final usage flush inside Kubernetes' 30-second termination grace period.
builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(25));

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddRedisMessaging(builder.Configuration);
builder.Services.AddEvaluationApi(builder.Configuration);

var app = builder.Build();
app.UseServiceDefaults();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapDefaultEndpoints();
app.MapSdkEndpoints();

await app.RunAsync();
