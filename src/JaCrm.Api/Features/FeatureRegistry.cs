using JaCrm.Api.Features.Settings;

namespace JaCrm.Api.Features;

/// <summary>Every feature module registers its services and endpoints here. Adding a module
/// (e.g. contacts, interviews) means one line in each method.</summary>
public static class FeatureRegistry
{
    public static IServiceCollection AddFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<SettingsService>();
        return services;
    }

    public static RouteGroupBuilder MapFeatureEndpoints(this RouteGroupBuilder api)
    {
        api.MapSettingsEndpoints();
        return api;
    }
}
