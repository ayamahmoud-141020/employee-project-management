using System.Linq.Expressions;

namespace EPM.Application.Common;

/// <summary>
/// The set of columns a list endpoint is willing to sort by.
/// </summary>
/// <remarks>
/// A whitelist, not a convenience. The alternative — building an OrderBy from whatever string
/// arrived in the query string — either needs dynamic LINQ (which happily sorts by a column
/// you never meant to expose, and can be coaxed into evaluating expressions) or string
/// concatenation into SQL. Here an unknown key silently falls back to the default sort, so
/// the worst a hostile `?sortBy=` can do is give the caller the default order.
///
/// Each entry stores a strongly typed selector rather than Expression&lt;Func&lt;T, object&gt;&gt;;
/// boxing the key would defeat SQL translation for value types like DateOnly.
/// </remarks>
public sealed class SortMap<T>
{
    private readonly Dictionary<string, Func<IQueryable<T>, bool, IOrderedQueryable<T>>> _sorts =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly string _defaultKey;

    private SortMap(string defaultKey) => _defaultKey = defaultKey;

    /// <param name="defaultKey">
    /// Applied when the caller asks for nothing, or for something not on the list. Must be
    /// registered with <see cref="Add"/> — an unsorted paged query returns rows in whatever
    /// order the database feels like, which makes page 2 non-deterministic.
    /// </param>
    public static SortMap<T> WithDefault(string defaultKey) => new(defaultKey);

    public SortMap<T> Add<TKey>(string key, Expression<Func<T, TKey>> selector)
    {
        _sorts[key] = (query, descending) => descending
            ? query.OrderByDescending(selector)
            : query.OrderBy(selector);

        return this;
    }

    /// <summary>The keys clients may pass, for documentation and error messages.</summary>
    public IReadOnlyCollection<string> AllowedKeys => _sorts.Keys;

    public IOrderedQueryable<T> Apply(IQueryable<T> query, string? requestedKey, bool descending)
    {
        if (requestedKey is not null && _sorts.TryGetValue(requestedKey, out var sort))
        {
            return sort(query, descending);
        }

        if (!_sorts.TryGetValue(_defaultKey, out var fallback))
        {
            throw new InvalidOperationException(
                $"Sort map for {typeof(T).Name} declares '{_defaultKey}' as its default but never registered it.");
        }

        return fallback(query, descending);
    }
}
