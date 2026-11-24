using Common.Paging;

namespace BonakMarket.StorefrontApi.Security;

// Paging operates only on the already scoped and projected public DTO query.
// Reject export, aggregate and arbitrary property paths on customer-facing routes.
internal static class StorefrontPaging
{
    public static bool Validate<T>(PagingFilter filter, params string[] fields)
    {
        if (filter == null || filter.PageSize < 1 || filter.PageSize > 100 ||
            filter.PageNumber < 1 || filter.PageNumber > 10000 ||
            filter.OutputType != OutputType.List ||
            filter.AggregateParams?.Count > 0 || filter.ColumnsInfo?.Count > 0 ||
            filter.FilterParam?.Count > 10 || filter.SortingParams?.Count > 5)
            return false;
        var allowed = new HashSet<string>(fields, StringComparer.OrdinalIgnoreCase);
        return (filter.FilterParam == null || filter.FilterParam.All(f =>
                    f != null && allowed.Contains(f.ColumnName ?? "") &&
                    f.FilterValue?.Length <= 256 && Enum.IsDefined(f.FilterOption))) &&
               (filter.SortingParams == null || filter.SortingParams.All(s =>
                    s != null && allowed.Contains(s.ColumnName ?? "") && Enum.IsDefined(s.SortOrder))) &&
               Enum.IsDefined(filter.ComparisonMode);
    }
}
