using JaCrm.Api.Common;
using JaCrm.Api.Data;
using JaCrm.Api.Data.Configurations;
using JaCrm.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JaCrm.Api.Features.Technologies;

/// <summary>Turns tag names from a form into Technology rows, reusing existing tags
/// (case-insensitive) and creating new ones.</summary>
public sealed class TechnologyResolver(AppDbContext db)
{
    public const int MaxTagsPerList = 40;

    /// <summary>Resolves both lists at once so a tag new to both lists is created only once.
    /// A tag listed as required is dropped from preferred.</summary>
    public async Task<(List<Technology> Required, List<Technology> Preferred)> ResolveAsync(
        IReadOnlyList<string>? required, IReadOnlyList<string>? preferred, Validator validator, CancellationToken ct)
    {
        var requiredNames = Clean(required, "requiredTechnologies", validator);
        var requiredKeys = requiredNames.Select(Technology.Normalize).ToHashSet(StringComparer.Ordinal);
        var preferredNames = Clean(preferred, "preferredTechnologies", validator)
            .Where(n => !requiredKeys.Contains(Technology.Normalize(n)))
            .ToList();
        validator.ThrowIfInvalid();

        var keys = requiredNames.Concat(preferredNames).Select(Technology.Normalize).Distinct().ToList();
        var known = await db.Technologies.Where(t => keys.Contains(t.NormalizedName)).ToDictionaryAsync(t => t.NormalizedName, ct);

        Technology Get(string name)
        {
            var key = Technology.Normalize(name);
            if (!known.TryGetValue(key, out var technology))
            {
                technology = Technology.Create(name);
                db.Technologies.Add(technology);
                known[key] = technology;
            }

            return technology;
        }

        return (requiredNames.Select(Get).ToList(), preferredNames.Select(Get).ToList());
    }

    private static List<string> Clean(IReadOnlyList<string>? names, string field, Validator validator)
    {
        var cleaned = (names ?? [])
            .Select(Validator.Clean)
            .OfType<string>()
            .DistinctBy(Technology.Normalize)
            .ToList();
        if (cleaned.Count > MaxTagsPerList)
        {
            validator.Add(field, $"Use at most {MaxTagsPerList} technologies.");
        }

        if (cleaned.Any(n => n.Length > Lengths.Short))
        {
            validator.Add(field, $"A technology name can be at most {Lengths.Short} characters.");
        }

        return cleaned;
    }
}
