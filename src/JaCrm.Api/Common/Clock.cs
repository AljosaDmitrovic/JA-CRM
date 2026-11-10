using Microsoft.Extensions.Options;

namespace JaCrm.Api.Common;

public sealed class AppOptions
{
    public const string Section = "App";

    /// <summary>Folder for the database, data-protection keys and backups. Relative paths resolve
    /// against the content root. Not "data": on macOS that is the same folder as the Data/ sources.</summary>
    public string DataDirectory { get; set; } = "App_Data";

    /// <summary>IANA time zone used for "today" (auto submission date, deadlines). Decision D-23.</summary>
    public string TimeZone { get; set; } = "Europe/Belgrade";

    /// <summary>Trust X-Forwarded-* headers. Enable only behind a reverse proxy that sets them.</summary>
    public bool TrustForwardedHeaders { get; set; }
}

/// <summary>The current time in UTC and today's date in the owner's time zone.</summary>
public sealed class Clock(TimeProvider timeProvider, IOptions<AppOptions> options)
{
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(UtcNow, _zone));
}
