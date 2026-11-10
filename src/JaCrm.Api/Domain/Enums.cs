namespace JaCrm.Api.Domain;

// Enums are persisted and serialized by name (see decisions D-12), so members can be reordered
// safely but must never be renamed without a data migration.

public enum ApplicationStatus
{
    Interested,
    Researching,
    ReadyToApply,
    Applied,
    RecruiterInterview,
    TechnicalInterview,
    HiringManagerInterview,
    FinalInterview,
    OfferReceived,
    Rejected,
    Withdrawn,
    NoResponse,
}

public enum ApplicationPhase
{
    Pipeline,
    Applied,
    Interviewing,
    Offer,
    Closed,
}

public enum Priority
{
    Low,
    Medium,
    High,
}

public enum CompanySize
{
    From1To10,
    From11To50,
    From51To200,
    From201To1000,
    From1001To5000,
    Over5000,
}

public enum Seniority
{
    Intern,
    Junior,
    Mid,
    Senior,
    Lead,
    Principal,
}

public enum VacancySource
{
    LinkedIn,
    CompanyWebsite,
    Referral,
    Recruiter,
    JobBoard,
    Other,
}

public enum WorkArrangement
{
    Remote,
    Hybrid,
    OnSite,
}

/// <summary>Yes / No / Unknown answers (open to my location, relocation support).</summary>
public enum TriState
{
    Unknown,
    Yes,
    No,
}

public enum EmploymentType
{
    FullTime,
    PartTime,
    Contractor,
    Freelance,
    Internship,
}

public enum PayPeriod
{
    Hourly,
    Monthly,
    Annual,
}

public enum SalaryType
{
    Gross,
    Net,
    Unknown,
}

public enum ThemePreference
{
    System,
    Light,
    Dark,
}

public enum DateFormatPreference
{
    /// <summary>dd.MM.yyyy</summary>
    DayMonthYear,

    /// <summary>yyyy-MM-dd</summary>
    Iso,
}
