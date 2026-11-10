namespace JaCrm.Api.Common;

public enum ArchivedFilter
{
    /// <summary>Default: archived records are hidden.</summary>
    Active,
    Archived,
    All,
}

public static class Text
{
    /// <summary>A SQL LIKE pattern for "contains", with wildcards in the term escaped (escape char '\').</summary>
    public static string? ContainsPattern(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return null;
        }

        var escaped = term.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }

    public const string LikeEscape = "\\";

    /// <summary>Adds https:// to a bare domain ("acme.com") so it can be stored as a URL.</summary>
    public static string? NormalizeUrl(string? value)
    {
        var cleaned = Validator.Clean(value);
        if (cleaned is null)
        {
            return null;
        }

        return cleaned.Contains("://", StringComparison.Ordinal) ? cleaned : "https://" + cleaned;
    }
}
