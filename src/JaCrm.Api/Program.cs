using System.Text.Json.Serialization;
using JaCrm.Api;
using JaCrm.Api.Cli;
using JaCrm.Api.Data;
using JaCrm.Api.Features.Auth;

if (Commands.IsHashPassword(args))
{
    return Commands.HashPassword();
}

if (Commands.IsHealthCheck(args))
{
    return await Commands.HealthCheckAsync();
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddJaCrm(builder.Configuration);
builder.Services.ConfigureHttpJsonOptions(json =>
{
    json.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    json.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});

var app = builder.Build();

if (Commands.IsHostCommand(args))
{
    return await Commands.RunHostCommandAsync(app.Services, args);
}

AuthSetup.EnsurePasswordConfigured(app.Services);
await DatabaseMaintenance.MigrateAsync(app.Services);

app.UseJaCrmPipeline();
app.MapJaCrmEndpoints();

await app.RunAsync();
return 0;

/// <summary>Exposed for <c>WebApplicationFactory</c> in the integration tests.</summary>
public partial class Program;
