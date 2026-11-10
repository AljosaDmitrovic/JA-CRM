namespace JaCrm.Api.Domain;

/// <summary>Human-readable labels for every enum. The UI and the CSV export both use these.</summary>
public static class EnumLabels
{
    public static string For<TEnum>(TEnum value) where TEnum : struct, Enum => value switch
    {
        ApplicationStatus s => s switch
        {
            ApplicationStatus.ReadyToApply => "Ready to Apply",
            ApplicationStatus.RecruiterInterview => "Recruiter Interview",
            ApplicationStatus.TechnicalInterview => "Technical Interview",
            ApplicationStatus.HiringManagerInterview => "Hiring Manager Interview",
            ApplicationStatus.FinalInterview => "Final Interview",
            ApplicationStatus.OfferReceived => "Offer Received",
            ApplicationStatus.NoResponse => "No Response",
            _ => s.ToString(),
        },
        CompanySize c => c switch
        {
            CompanySize.From1To10 => "1–10",
            CompanySize.From11To50 => "11–50",
            CompanySize.From51To200 => "51–200",
            CompanySize.From201To1000 => "201–1,000",
            CompanySize.From1001To5000 => "1,001–5,000",
            _ => "5,000+",
        },
        VacancySource s => s switch
        {
            VacancySource.CompanyWebsite => "Company website",
            VacancySource.JobBoard => "Job board",
            _ => s.ToString(),
        },
        WorkArrangement w => w == WorkArrangement.OnSite ? "On-site" : w.ToString(),
        EmploymentType e => e switch
        {
            EmploymentType.FullTime => "Full-time employee",
            EmploymentType.PartTime => "Part-time employee",
            EmploymentType.Contractor => "Contractor / B2B",
            _ => e.ToString(),
        },
        DateFormatPreference d => d == DateFormatPreference.Iso ? "yyyy-MM-dd" : "dd.MM.yyyy",
        _ => value.ToString(),
    };

    public static string? For<TEnum>(TEnum? value) where TEnum : struct, Enum =>
        value is { } v ? For(v) : null;
}
