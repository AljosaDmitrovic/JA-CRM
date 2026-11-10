using System.Text.RegularExpressions;

namespace JaCrm.Api.Common;

/// <summary>Collects field errors and throws one <see cref="ValidationException"/>. Server-side
/// validation is authoritative; the Angular forms only mirror it for fast feedback.</summary>
public sealed partial class Validator
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

    public bool IsValid => _errors.Count == 0;

    public Validator Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var list))
        {
            _errors[field] = list = [];
        }

        list.Add(message);
        return this;
    }

    public Validator Required(string field, string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(field, $"{label} is required.");
        }

        return this;
    }

    public Validator MaxLength(string field, string? value, int max, string label)
    {
        if (value is not null && value.Trim().Length > max)
        {
            Add(field, $"{label} must be at most {max} characters.");
        }

        return this;
    }

    public Validator HttpUrl(string field, string? value, string label)
    {
        if (!string.IsNullOrWhiteSpace(value) && !IsHttpUrl(value))
        {
            Add(field, $"{label} must be a valid http(s) URL.");
        }

        return this;
    }

    public Validator Range(string field, decimal? value, decimal min, decimal max, string label)
    {
        if (value is { } v && (v < min || v > max))
        {
            Add(field, $"{label} must be between {min} and {max}.");
        }

        return this;
    }

    public Validator Currency(string field, string? value, string label = "Currency")
    {
        if (value is null || !CurrencyPattern().IsMatch(value))
        {
            Add(field, $"{label} must be a 3-letter ISO 4217 code, e.g. EUR.");
        }

        return this;
    }

    public Validator Defined<TEnum>(string field, TEnum? value, string label) where TEnum : struct, Enum
    {
        if (value is { } v && !Enum.IsDefined(v))
        {
            Add(field, $"{label} has an unknown value.");
        }

        return this;
    }

    public void ThrowIfInvalid()
    {
        if (!IsValid)
        {
            throw new ValidationException(_errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
        }
    }

    public static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>Trims and turns whitespace-only strings into null.</summary>
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyPattern();
}
