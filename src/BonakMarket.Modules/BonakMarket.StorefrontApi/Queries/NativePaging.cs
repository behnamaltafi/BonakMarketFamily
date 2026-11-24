using BonakMarket.Collections;
using BonakMarket.StorefrontApi.Models;

namespace BonakMarket.StorefrontApi.Queries;

internal static class NativePaging
{
    public static async Task<PageDto<T>> PageAsync<T>(this IQueryable<T> query, PageRequest request, CancellationToken ct)
    {
        var page = await query.ToPagedList(request.Page - 1, request.PageSize).LoadAsync(cancelToken: ct);
        return Map(page, page.ToList());
    }

    public static PageDto<TDto> Map<T, TDto>(IPagedList<T> page, IReadOnlyList<TDto> items) =>
        new(items, page.PageNumber, page.PageSize, page.TotalCount, page.TotalPages);
}
