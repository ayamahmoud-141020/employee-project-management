using Microsoft.EntityFrameworkCore;

namespace EPM.Application.Common;

public static class QueryableExtensions
{
    /// <summary>
    /// Runs a count and a page fetch against the same filtered query and packages both.
    /// </summary>
    /// <remarks>
    /// Everything here happens in SQL: the COUNT and the OFFSET/FETCH both go to the server,
    /// so a 50,000-row employee table never lands in application memory. That is the whole
    /// point of paging server-side — calling ToListAsync() first and paging the list afterwards
    /// gives identical output and a completely different performance story.
    ///
    /// The count is issued first and separately, because it must reflect the filters but not
    /// the paging.
    /// </remarks>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PagingOptions paging,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);

        // Asking for page 9 of a 3-page result is not an error, it is just empty. Skipping the
        // second round trip saves the database a pointless read.
        if (totalCount == 0)
        {
            return PagedResult<T>.Empty(paging.Page, paging.PageSize);
        }

        var items = await query
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, paging.Page, paging.PageSize, totalCount);
    }

    /// <summary>
    /// Applies a filter only when <paramref name="condition"/> holds. Keeps list handlers
    /// free of `if (x is not null) query = query.Where(...)` repeated six times.
    /// </summary>
    public static IQueryable<T> WhereIf<T>(
        this IQueryable<T> query,
        bool condition,
        System.Linq.Expressions.Expression<Func<T, bool>> predicate) =>
        condition ? query.Where(predicate) : query;
}
