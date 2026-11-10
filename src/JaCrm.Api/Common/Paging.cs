namespace JaCrm.Api.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public static class Paging
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public static PagedResult<T> Page<T>(IReadOnlyList<T> sorted, int? page, int? pageSize)
    {
        var size = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        var number = Math.Max(page ?? 1, 1);
        var items = sorted.Skip((number - 1) * size).Take(size).ToList();
        return new PagedResult<T>(items, sorted.Count, number, size);
    }

    public static bool Descending(string? direction) => string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);

    /// <summary>Orders with a key and direction; nulls always last.</summary>
    public static IEnumerable<T> OrderNullsLast<T, TKey>(this IEnumerable<T> source, Func<T, TKey?> key, bool descending)
        where TKey : struct, IComparable<TKey>
    {
        var withValue = source.Where(x => key(x).HasValue);
        var ordered = descending ? withValue.OrderByDescending(x => key(x)!.Value) : withValue.OrderBy(x => key(x)!.Value);
        return ordered.Concat(source.Where(x => !key(x).HasValue));
    }

    public static IEnumerable<T> OrderText<T>(this IEnumerable<T> source, Func<T, string?> key, bool descending) =>
        descending
            ? source.OrderByDescending(key, StringComparer.CurrentCultureIgnoreCase)
            : source.OrderBy(key, StringComparer.CurrentCultureIgnoreCase);
}
