using JaCrm.Api.Domain;

namespace JaCrm.Api.Features.Meta;

public sealed record Option(string Value, string Label);

public sealed record StatusOption(string Value, string Label, string Phase);

public sealed record PhaseOption(string Value, string Label, IReadOnlyList<string> Statuses);

/// <summary>Enum values, labels and the status → phase map, so the UI never duplicates them.</summary>
public sealed record MetaResponse(
    IReadOnlyList<StatusOption> Statuses,
    IReadOnlyList<PhaseOption> Phases,
    IReadOnlyList<Option> Priorities,
    IReadOnlyList<Option> CompanySizes,
    IReadOnlyList<Option> Seniorities,
    IReadOnlyList<Option> Sources,
    IReadOnlyList<Option> WorkArrangements,
    IReadOnlyList<Option> TriStates,
    IReadOnlyList<Option> EmploymentTypes,
    IReadOnlyList<Option> PayPeriods,
    IReadOnlyList<Option> SalaryTypes,
    IReadOnlyList<Option> Themes,
    IReadOnlyList<Option> DateFormats,
    IReadOnlyList<string> CommonCurrencies);

public static class MetaEndpoints
{
    private static readonly MetaResponse Meta = Build();

    public static RouteGroupBuilder MapMetaEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/meta", () => Meta).WithTags("Meta");
        return api;
    }

    private static MetaResponse Build() => new(
        ApplicationStatuses.All.Select(s => new StatusOption(s.ToString(), EnumLabels.For(s), ApplicationStatuses.PhaseOf(s).ToString())).ToArray(),
        ApplicationStatuses.Phases.Select(p => new PhaseOption(
            p.ToString(), EnumLabels.For(p), ApplicationStatuses.InPhase(p).Select(s => s.ToString()).ToArray())).ToArray(),
        Options<Priority>(),
        Options<CompanySize>(),
        Options<Seniority>(),
        Options<VacancySource>(),
        Options<WorkArrangement>(),
        Options<TriState>(),
        Options<EmploymentType>(),
        Options<PayPeriod>(),
        Options<SalaryType>(),
        Options<ThemePreference>(),
        Options<DateFormatPreference>(),
        ["EUR", "CHF", "RSD", "USD", "GBP", "SEK", "NOK", "DKK", "PLN", "CZK", "HUF", "RON", "BGN", "CAD", "AUD"]);

    private static Option[] Options<TEnum>() where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>().Select(v => new Option(v.ToString(), EnumLabels.For(v))).ToArray();
}
