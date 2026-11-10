using JaCrm.Api.Features.Applications;
using JaCrm.Api.Features.Companies;
using JaCrm.Api.Features.Dashboard;
using JaCrm.Api.Features.DataTransfer;
using JaCrm.Api.Features.Search;
using JaCrm.Api.Features.Settings;
using JaCrm.Api.Features.Technologies;
using JaCrm.Api.Features.Vacancies;

namespace JaCrm.Api.Features;

/// <summary>Every feature module registers its services and endpoints here. Adding a module
/// (e.g. contacts, interviews) means one line in each method.</summary>
public static class FeatureRegistry
{
    public static IServiceCollection AddFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<SettingsService>();
        services.AddScoped<TechnologyResolver>();
        services.AddScoped<CompanyService>();
        services.AddScoped<VacancyService>();
        services.AddScoped<ApplicationService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<SearchService>();
        services.AddScoped<DataTransferService>();
        services.AddScoped<CsvExportService>();
        return services;
    }

    public static RouteGroupBuilder MapFeatureEndpoints(this RouteGroupBuilder api)
    {
        api.MapSettingsEndpoints();
        api.MapCompanyEndpoints();
        api.MapVacancyEndpoints();
        api.MapApplicationEndpoints();
        api.MapDashboardEndpoints();
        api.MapSearchEndpoints();
        api.MapDataTransferEndpoints();
        return api;
    }
}
