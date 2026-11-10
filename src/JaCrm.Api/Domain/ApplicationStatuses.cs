namespace JaCrm.Api.Domain;

/// <summary>The status → phase map and the rules derived from it. The single source of truth:
/// the UI receives the same grouping through <c>GET /api/meta</c>.</summary>
public static class ApplicationStatuses
{
    public static IReadOnlyList<ApplicationStatus> All { get; } = Enum.GetValues<ApplicationStatus>();

    public static IReadOnlyList<ApplicationPhase> Phases { get; } = Enum.GetValues<ApplicationPhase>();

    public static ApplicationPhase PhaseOf(ApplicationStatus status) => status switch
    {
        ApplicationStatus.Interested or ApplicationStatus.Researching or ApplicationStatus.ReadyToApply
            => ApplicationPhase.Pipeline,
        ApplicationStatus.Applied => ApplicationPhase.Applied,
        ApplicationStatus.RecruiterInterview or ApplicationStatus.TechnicalInterview
            or ApplicationStatus.HiringManagerInterview or ApplicationStatus.FinalInterview
            => ApplicationPhase.Interviewing,
        ApplicationStatus.OfferReceived => ApplicationPhase.Offer,
        ApplicationStatus.Rejected or ApplicationStatus.Withdrawn or ApplicationStatus.NoResponse
            => ApplicationPhase.Closed,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    public static IReadOnlyList<ApplicationStatus> InPhase(ApplicationPhase phase) =>
        All.Where(s => PhaseOf(s) == phase).ToArray();

    /// <summary>Applied, any interview status, or Offer Received.</summary>
    public static bool IsActive(ApplicationStatus status) =>
        PhaseOf(status) is ApplicationPhase.Applied or ApplicationPhase.Interviewing or ApplicationPhase.Offer;

    public static bool IsInterview(ApplicationStatus status) => PhaseOf(status) == ApplicationPhase.Interviewing;

    /// <summary>Entering one of these statuses means the application has been submitted, so an
    /// empty submission date is filled in. Closed statuses are excluded on purpose: an application
    /// can be withdrawn or rejected before it was ever sent (decision D-20).</summary>
    public static bool ImpliesSubmission(ApplicationStatus status) => IsActive(status);
}
