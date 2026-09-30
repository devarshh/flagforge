using FlagForge.Application;
using FlagForge.Infrastructure;
using FlagForge.ServiceDefaults;
using FlagForge.Worker;

// A web host only for its health endpoints; the work happens in the hosted jobs.
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddRedisMessaging(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddWorkerJobs(builder.Configuration);

var app = builder.Build();
app.UseServiceDefaults();
app.MapDefaultEndpoints();

await app.RunAsync();
