namespace EPM.Application.Common;

/// <summary>
/// One page of results plus what the client needs to render a pager.
/// </summary>
/// <remarks>
/// TotalCount is the count of matching rows before paging, not the size of Items — the table
/// footer needs to say "showing 1-20 of 347", which is impossible if the server only ever
/// reports what it sent.
/// </remarks>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;

    public static PagedResult<T> Empty(int page, int pageSize) => new([], page, pageSize, 0);
}
