using JaCrm.Api.Domain;

namespace JaCrm.UnitTests.Domain;

public sealed class ApplicationStatusTests
{
    private static readonly DateTime Created = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly CreatedOn = new(2026, 10, 1);

    [Theory]
    [InlineData(ApplicationStatus.Interested, ApplicationPhase.Pipeline)]
    [InlineData(ApplicationStatus.Researching, ApplicationPhase.Pipeline)]
    [InlineData(ApplicationStatus.ReadyToApply, ApplicationPhase.Pipeline)]
    [InlineData(ApplicationStatus.Applied, ApplicationPhase.Applied)]
    [InlineData(ApplicationStatus.RecruiterInterview, ApplicationPhase.Interviewing)]
    [InlineData(ApplicationStatus.TechnicalInterview, ApplicationPhase.Interviewing)]
    [InlineData(ApplicationStatus.HiringManagerInterview, ApplicationPhase.Interviewing)]
    [InlineData(ApplicationStatus.FinalInterview, ApplicationPhase.Interviewing)]
    [InlineData(ApplicationStatus.OfferReceived, ApplicationPhase.Offer)]
    [InlineData(ApplicationStatus.Rejected, ApplicationPhase.Closed)]
    [InlineData(ApplicationStatus.Withdrawn, ApplicationPhase.Closed)]
    [InlineData(ApplicationStatus.NoResponse, ApplicationPhase.Closed)]
    public void Every_status_belongs_to_its_phase(ApplicationStatus status, ApplicationPhase phase) =>
        Assert.Equal(phase, ApplicationStatuses.PhaseOf(status));

    [Fact]
    public void Creating_records_the_initial_status_and_snapshots_the_posting()
    {
        var vacancy = Vacancy("Senior .NET Developer", "Acme");
        vacancy.RequiredTechnologies.AddRange([Technology.Create("Kubernetes"), Technology.Create("C#")]);

        var application = Application.Create(vacancy, ApplicationStatus.Interested, Priority.High, Created, CreatedOn);

        var entry = Assert.Single(application.StatusHistory);
        Assert.Null(entry.FromStatus);
        Assert.Equal(ApplicationStatus.Interested, entry.ToStatus);
        Assert.Equal(Created, application.StatusChangedAt);
        Assert.Null(application.SubmittedOn);
        Assert.Equal("Senior .NET Developer", application.PostingSnapshot.JobTitle);
        Assert.Equal("Acme", application.PostingSnapshot.CompanyName);
        Assert.Equal(["C#", "Kubernetes"], application.PostingSnapshot.RequiredTechnologies);
    }

    [Fact]
    public void Creating_directly_in_a_submitted_status_sets_the_submission_date()
    {
        var application = Application.Create(Vacancy(), ApplicationStatus.Applied, Priority.Medium, Created, CreatedOn);

        Assert.Equal(CreatedOn, application.SubmittedOn);
    }

    [Fact]
    public void Any_status_can_move_to_any_other_including_backwards()
    {
        var application = Application.Create(Vacancy(), ApplicationStatus.FinalInterview, Priority.Medium, Created, CreatedOn);

        application.ChangeStatus(ApplicationStatus.Researching, "Role re-opened", Created.AddDays(1), CreatedOn.AddDays(1));
        application.ChangeStatus(ApplicationStatus.OfferReceived, null, Created.AddDays(2), CreatedOn.AddDays(2));

        Assert.Equal(ApplicationStatus.OfferReceived, application.Status);
        Assert.Equal(3, application.StatusHistory.Count);
        var backwards = application.StatusHistory[1];
        Assert.Equal(ApplicationStatus.FinalInterview, backwards.FromStatus);
        Assert.Equal(ApplicationStatus.Researching, backwards.ToStatus);
        Assert.Equal("Role re-opened", backwards.Note);
    }

    [Theory]
    [InlineData(ApplicationStatus.Applied)]
    [InlineData(ApplicationStatus.RecruiterInterview)]
    [InlineData(ApplicationStatus.FinalInterview)]
    [InlineData(ApplicationStatus.OfferReceived)]
    public void Entering_applied_or_later_sets_an_empty_submission_date(ApplicationStatus to)
    {
        var application = Application.Create(Vacancy(), ApplicationStatus.ReadyToApply, Priority.Medium, Created, CreatedOn);
        var today = CreatedOn.AddDays(3);

        application.ChangeStatus(to, null, Created.AddDays(3), today);

        Assert.Equal(today, application.SubmittedOn);
    }

    [Fact]
    public void An_existing_submission_date_is_never_overwritten()
    {
        var application = Application.Create(Vacancy(), ApplicationStatus.Interested, Priority.Medium, Created, CreatedOn);
        var submitted = new DateOnly(2026, 9, 20);
        application.SubmittedOn = submitted;

        application.ChangeStatus(ApplicationStatus.Applied, null, Created.AddDays(1), CreatedOn.AddDays(1));

        Assert.Equal(submitted, application.SubmittedOn);
    }

    [Theory]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Withdrawn)]
    [InlineData(ApplicationStatus.NoResponse)]
    [InlineData(ApplicationStatus.Researching)]
    public void Pipeline_and_closed_statuses_do_not_imply_submission(ApplicationStatus to)
    {
        var application = Application.Create(Vacancy(), ApplicationStatus.Interested, Priority.Medium, Created, CreatedOn);

        application.ChangeStatus(to, null, Created.AddDays(1), CreatedOn.AddDays(1));

        Assert.Null(application.SubmittedOn);
    }

    [Fact]
    public void Moving_to_the_current_status_is_rejected()
    {
        var application = Application.Create(Vacancy(), ApplicationStatus.Applied, Priority.Medium, Created, CreatedOn);

        Assert.Throws<InvalidOperationException>(() => application.ChangeStatus(ApplicationStatus.Applied, null, Created, CreatedOn));
        Assert.Single(application.StatusHistory);
    }

    [Fact]
    public void A_status_change_updates_the_staleness_clock()
    {
        var application = Application.Create(Vacancy(), ApplicationStatus.Applied, Priority.Medium, Created, CreatedOn);
        var later = Created.AddDays(5);

        application.ChangeStatus(ApplicationStatus.RecruiterInterview, null, later, CreatedOn.AddDays(5));

        Assert.Equal(later, application.StatusChangedAt);
        Assert.Equal(later, application.UpdatedAt);
    }

    private static Vacancy Vacancy(string title = "Developer", string company = "Acme")
    {
        var owner = new Company();
        owner.Rename(company);
        return new Vacancy { Title = title, Company = owner };
    }
}
