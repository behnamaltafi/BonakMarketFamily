#nullable enable

namespace BonakMarket.Identity.Models;

/// <summary>Public location page using the standard storefront pagination metadata.</summary>
public sealed record LocationPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages)
{
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
