using System.Text;

namespace JaCrm.Api.Domain;

/// <summary>Normalization used to warn about likely duplicate companies: same normalized name
/// (case, whitespace, punctuation and legal suffixes ignored) or same website domain.</summary>
public static class CompanyMatching
{
    // Decision D-29: the spec's suffixes plus common EU legal forms. Compared after punctuation is
    // removed, so "B.V." → "bv" and "d.o.o." → "doo".
    private static readonly HashSet<string> LegalSuffixes = new(StringComparer.Ordinal)
    {
        "inc", "incorporated", "ltd", "limited", "llc", "gmbh", "ag", "sa", "bv", "nv", "doo", "ad",
        "plc", "corp", "corporation", "co", "company", "sarl", "srl", "spa", "ab", "as", "oy", "kg", "se",
    };

    /// <summary>"Acme, Inc." → "acme"; "Nordic Code d.o.o." → "nordiccode".</summary>
    public static string NormalizeName(string name)
    {
        var words = new List<string>();
        foreach (var rawWord in name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var word = KeepLettersAndDigits(rawWord);
            if (word.Length > 0)
            {
                words.Add(word);
            }
        }

        // Strip trailing legal suffixes ("Acme Holding GmbH & Co KG" → "acmeholding"), but never the
        // whole name: a company literally called "AG" keeps its name.
        while (words.Count > 1 && LegalSuffixes.Contains(words[^1]))
        {
            words.RemoveAt(words.Count - 1);
        }

        return string.Concat(words);
    }

    /// <summary>"https://www.Acme.com/careers" → "acme.com". Returns null for empty or unparseable input.</summary>
    public static string? NormalizeDomain(string? website)
    {
        if (string.IsNullOrWhiteSpace(website))
        {
            return null;
        }

        var candidate = website.Trim();
        if (!candidate.Contains("://", StringComparison.Ordinal))
        {
            candidate = "https://" + candidate;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
        {
            return null;
        }

        var host = uri.IdnHost.ToLowerInvariant().TrimEnd('.');
        return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
    }

    private static string KeepLettersAndDigits(string word)
    {
        var builder = new StringBuilder(word.Length);
        foreach (var c in word.Normalize(NormalizationForm.FormD))
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }
}
