namespace EPM.Application.Common;

/// <summary>
/// The paging and sorting parameters every list endpoint accepts.
/// </summary>
/// <remarks>
/// A record with defaults rather than an interface each query implements: the values are
/// identical everywhere, and the individual queries only differ in what they let you filter
/// and sort by. Queries compose this instead of inheriting from it, so a query stays a flat
/// record that MediatR and Swagger can both describe easily.
/// </remarks>
public sealed record PagingOptions
{
    public const int DefaultPageSize = 20;

    /// <summary>
    /// Hard ceiling on page size. Without one, `?pageSize=1000000` is a free denial of
    /// service — the client controls how much work the database does.
    /// </summary>
    public const int MaxPageSize = 200;

    /// <summary>One-based, because that is what a pager displays.</summary>
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>Free-text search. What it matches is decided per list slice.</summary>
    public string? Search { get; init; }

    /// <summary>
    /// Must be one of the keys the slice's sort map allows. Anything else falls back to the
    /// default — the column name never reaches a query as raw text.
    /// </summary>
    public string? SortBy { get; init; }

    public bool SortDescending { get; init; }

    /// <summary>
    /// Clamps hostile or careless input into a usable range. Called by handlers before the
    /// values touch a query, so a bad page number degrades to page 1 instead of a 500.
    /// </summary>
    public PagingOptions Normalised() => this with
    {
        Page = Page < 1 ? 1 : Page,
        PageSize = PageSize switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => PageSize,
        },
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
    };
}
