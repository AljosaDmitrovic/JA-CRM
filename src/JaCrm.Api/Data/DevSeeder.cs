using JaCrm.Api.Common;
using JaCrm.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JaCrm.Api.Data;

/// <summary>Realistic demonstration data with fictional companies. Runs only through the explicit
/// <c>seed</c> command and only against an empty database (decision D-41).</summary>
public static class DevSeeder
{
    public static async Task<bool> SeedAsync(AppDbContext db, Clock clock, CancellationToken ct = default)
    {
        if (await db.Companies.AnyAsync(ct) || await db.Vacancies.AnyAsync(ct) || await db.Applications.AnyAsync(ct))
        {
            return false;
        }

        var now = clock.UtcNow;
        var today = clock.Today;
        DateTime DaysAgo(int days) => now.AddDays(-days);

        db.ExchangeRates.AddRange(
            new ExchangeRate { Currency = "CHF", RateToBase = 1.06m, UpdatedOn = today },
            new ExchangeRate { Currency = "RSD", RateToBase = 0.00854m, UpdatedOn = today },
            new ExchangeRate { Currency = "USD", RateToBase = 0.92m, UpdatedOn = today },
            new ExchangeRate { Currency = "GBP", RateToBase = 1.17m, UpdatedOn = today });

        var tech = new Dictionary<string, Technology>(StringComparer.OrdinalIgnoreCase);
        Technology T(string name) => tech.TryGetValue(name, out var t) ? t : tech[name] = Technology.Create(name);

        Company NewCompany(string name, string? website, string country, string city, string industry, CompanySize size, int? rating, int addedDaysAgo, string? notes = null)
        {
            var company = new Company
            {
                Country = country,
                City = city,
                Industry = industry,
                Headquarters = $"{city}, {country}",
                Size = size,
                Rating = rating,
                Notes = notes,
                CareersUrl = website is null ? null : $"{website}/careers",
                CreatedAt = DaysAgo(addedDaysAgo),
                UpdatedAt = DaysAgo(addedDaysAgo),
            };
            company.Rename(name);
            company.SetWebsite(website);
            db.Companies.Add(company);
            return company;
        }

        var alpine = NewCompany("Alpenwerk Software AG", "https://www.alpenwerk.example", "Switzerland", "Zurich", "Fintech", CompanySize.From201To1000, 5, 40,
            "Strong engineering blog. Pays **CHF**, hybrid in Zurich.\n\n- 13th salary\n- 5 weeks vacation");
        var nordlys = NewCompany("Nordlys Data AS", "https://nordlys.example", "Norway", "Oslo", "Energy analytics", CompanySize.From51To200, 4, 35);
        var kanal = NewCompany("Kanal Logistics B.V.", "https://kanal-logistics.example", "Netherlands", "Rotterdam", "Logistics", CompanySize.From1001To5000, 3, 30);
        var dunav = NewCompany("Dunav Code d.o.o.", "https://dunavcode.example", "Serbia", "Novi Sad", "Software services", CompanySize.From51To200, 4, 28,
            "Local office with foreign clients. Salary in EUR, paid in RSD.");
        var brightline = NewCompany("Brightline Health Ltd", "https://brightline.example", "United Kingdom", "London", "Healthtech", CompanySize.From201To1000, 3, 25);
        var vela = NewCompany("Vela Payments GmbH", "https://vela-payments.example", "Germany", "Berlin", "Payments", CompanySize.From51To200, 4, 20);
        var quarry = NewCompany("Quarry Labs", "https://quarrylabs.example", "Estonia", "Tallinn", "Developer tools", CompanySize.From11To50, 5, 12);
        var stari = NewCompany("Stari Grad Games", null, "Serbia", "Belgrade", "Gaming", CompanySize.From11To50, 2, 60);
        stari.Archive(DaysAgo(15));

        Vacancy NewVacancy(Company company, string title, Seniority seniority, WorkArrangement arrangement, EmploymentType type,
            string[] required, string[] preferred, SalaryRange? salary, int discoveredDaysAgo, int? deadlineInDays,
            TriState openToMe, TriState relocation, string? eligibility, VacancySource source, string? description = null)
        {
            var vacancy = new Vacancy
            {
                Company = company,
                Title = title,
                Seniority = seniority,
                WorkArrangement = arrangement,
                EmploymentType = type,
                PostedSalary = salary,
                Country = company.Country,
                City = company.City,
                RemoteEligibility = eligibility,
                OpenToMyLocation = openToMe,
                RelocationSupport = relocation,
                RelocationNotes = relocation == TriState.Yes ? "Relocation package and visa support." : null,
                Source = source,
                Url = company.Website is null ? null : $"{company.Website}/jobs/{title.ToLowerInvariant().Replace(' ', '-').Replace("/", "", StringComparison.Ordinal).Replace(".", "", StringComparison.Ordinal)}",
                RequiredExperienceYears = seniority >= Seniority.Senior ? 5 : 3,
                RequiredExperienceText = seniority >= Seniority.Senior ? "5+ years building backend services in production." : null,
                DateDiscovered = today.AddDays(-discoveredDaysAgo),
                ApplicationDeadline = deadlineInDays is { } d ? today.AddDays(d) : null,
                Description = description ?? $"""
                    ## About the role

                    We are looking for a **{title}** to join our product engineering team.

                    ## What you will do

                    - Design and build services in {string.Join(", ", required)}
                    - Own features end to end, from design to production
                    - Review code and help shape our engineering practices

                    ## What we offer

                    - Flexible hours
                    - Learning budget
                    """,
                CreatedAt = DaysAgo(discoveredDaysAgo),
                UpdatedAt = DaysAgo(discoveredDaysAgo),
            };
            vacancy.RequiredTechnologies.AddRange(required.Select(T));
            vacancy.PreferredTechnologies.AddRange(preferred.Select(T));
            db.Vacancies.Add(vacancy);
            return vacancy;
        }

        var vAlpine = NewVacancy(alpine, "Senior .NET Engineer", Seniority.Senior, WorkArrangement.Hybrid, EmploymentType.FullTime,
            ["C#", ".NET", "Azure"], ["Kubernetes", "Kafka"], new SalaryRange(120_000, 140_000, "CHF", PayPeriod.Annual, SalaryType.Gross),
            38, null, TriState.No, TriState.Yes, null, VacancySource.LinkedIn);
        var vNordlys = NewVacancy(nordlys, "Backend Developer (C#)", Seniority.Senior, WorkArrangement.Remote, EmploymentType.FullTime,
            [".NET", "PostgreSQL", "Azure"], ["Angular"], new SalaryRange(70_000, 85_000, "EUR", PayPeriod.Annual, SalaryType.Gross),
            33, null, TriState.Yes, TriState.Unknown, "EU only", VacancySource.CompanyWebsite);
        var vKanal = NewVacancy(kanal, "Full-stack Engineer .NET / Angular", Seniority.Senior, WorkArrangement.Hybrid, EmploymentType.FullTime,
            [".NET", "Angular", "SQL Server"], ["Azure Service Bus"], new SalaryRange(5_500, 6_800, "EUR", PayPeriod.Monthly, SalaryType.Gross),
            29, null, TriState.No, TriState.Yes, null, VacancySource.Recruiter);
        var vDunav = NewVacancy(dunav, "Senior Software Engineer", Seniority.Senior, WorkArrangement.Hybrid, EmploymentType.FullTime,
            ["C#", ".NET", "Angular"], ["Docker"], new SalaryRange(3_800, 4_500, "EUR", PayPeriod.Monthly, SalaryType.Net),
            27, null, TriState.Yes, TriState.No, "Serbia", VacancySource.Referral);
        var vBright = NewVacancy(brightline, "Senior Backend Engineer", Seniority.Senior, WorkArrangement.Remote, EmploymentType.Contractor,
            ["C#", "AWS"], ["Terraform"], new SalaryRange(55, 65, "GBP", PayPeriod.Hourly, SalaryType.Unknown),
            24, null, TriState.Yes, TriState.No, "CET ±2h", VacancySource.JobBoard);
        var vVela = NewVacancy(vela, "Lead Engineer, Payments Core", Seniority.Lead, WorkArrangement.OnSite, EmploymentType.FullTime,
            ["C#", ".NET", "Kafka"], ["PostgreSQL"], new SalaryRange(90_000, 110_000, "EUR", PayPeriod.Annual, SalaryType.Gross),
            18, null, TriState.No, TriState.Yes, null, VacancySource.LinkedIn);
        var vQuarry = NewVacancy(quarry, "Senior Platform Engineer", Seniority.Senior, WorkArrangement.Remote, EmploymentType.FullTime,
            [".NET", "Kubernetes"], ["Go"], new SalaryRange(100_000, 130_000, "USD", PayPeriod.Annual, SalaryType.Gross),
            10, 12, TriState.Yes, TriState.No, "Worldwide", VacancySource.CompanyWebsite);
        var vStari = NewVacancy(stari, "Backend Engineer", Seniority.Mid, WorkArrangement.OnSite, EmploymentType.FullTime,
            ["C#"], [], new SalaryRange(250_000, 320_000, "RSD", PayPeriod.Monthly, SalaryType.Net),
            58, null, TriState.Yes, TriState.No, null, VacancySource.Other);
        NewVacancy(nordlys, "Senior Data Engineer", Seniority.Senior, WorkArrangement.Remote, EmploymentType.FullTime,
            ["Python", "Azure"], ["Databricks"], null, 6, 20, TriState.Unknown, TriState.Unknown, "EU only", VacancySource.LinkedIn);
        NewVacancy(vela, "Senior Angular Developer", Seniority.Senior, WorkArrangement.Hybrid, EmploymentType.FullTime,
            ["Angular", "TypeScript"], ["RxJS"], new SalaryRange(75_000, null, "EUR", PayPeriod.Annual, SalaryType.Gross),
            3, 8, TriState.No, TriState.Yes, null, VacancySource.Recruiter);

        await db.SaveChangesAsync(ct);

        Domain.Application Apply(Vacancy vacancy, int createdDaysAgo, Priority priority, params (ApplicationStatus Status, int DaysAgo, string? Note)[] path)
        {
            var created = DaysAgo(createdDaysAgo);
            var application = Domain.Application.Create(vacancy, ApplicationStatus.Interested, priority, created, DateOnly.FromDateTime(created));
            foreach (var (status, daysAgo, note) in path)
            {
                application.ChangeStatus(status, note, DaysAgo(daysAgo), DateOnly.FromDateTime(DaysAgo(daysAgo)));
            }

            application.CvVersion = "CV v4 — EU";
            application.ReasonForApplying = "Strong .NET product team with a clear domain.";
            db.Applications.Add(application);
            return application;
        }

        var aAlpine = Apply(vAlpine, 36, Priority.High,
            (ApplicationStatus.Applied, 34, null),
            (ApplicationStatus.RecruiterInterview, 26, "30 min call, good fit on salary."),
            (ApplicationStatus.TechnicalInterview, 15, "System design + live coding."),
            (ApplicationStatus.OfferReceived, 3, "Verbal offer, written contract pending."));
        aAlpine.ExpectedSalary = new SalaryValue(130_000, "CHF", PayPeriod.Annual, SalaryType.Gross);
        aAlpine.OfferedSalary = new SalaryValue(128_000, "CHF", PayPeriod.Annual, SalaryType.Gross);
        aAlpine.CompensationNotes = "13th salary included. 5 weeks vacation. Relocation budget CHF 8,000.";
        aAlpine.Notes = "## Interview notes\n\n- Team of 7, owns the settlement engine\n- On-call one week in six";

        Apply(vNordlys, 30, Priority.High, (ApplicationStatus.Applied, 29, null), (ApplicationStatus.RecruiterInterview, 20, null), (ApplicationStatus.TechnicalInterview, 9, "Take-home task submitted."))
            .ExpectedSalary = new SalaryValue(80_000, "EUR", PayPeriod.Annual, SalaryType.Gross);
        Apply(vKanal, 27, Priority.Medium, (ApplicationStatus.Applied, 25, null));
        Apply(vDunav, 26, Priority.Medium, (ApplicationStatus.Applied, 24, null), (ApplicationStatus.Rejected, 12, "Position filled internally."));
        Apply(vBright, 22, Priority.Low, (ApplicationStatus.Applied, 5, null));
        Apply(vVela, 16, Priority.High, (ApplicationStatus.Researching, 15, null), (ApplicationStatus.ReadyToApply, 2, null));
        Apply(vQuarry, 8, Priority.Medium);
        var aStari = Apply(vStari, 56, Priority.Low, (ApplicationStatus.Applied, 55, null), (ApplicationStatus.NoResponse, 20, null));
        aStari.Archive(DaysAgo(15));

        await db.SaveChangesAsync(ct);
        return true;
    }
}
