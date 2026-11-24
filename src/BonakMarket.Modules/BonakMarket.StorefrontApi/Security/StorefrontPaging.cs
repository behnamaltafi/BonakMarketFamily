using Common.Paging;
using Common.Paging.Enums;
using System.Globalization;
using System.Reflection;

namespace BonakMarket.StorefrontApi.Security;

// Paging operates only on the already scoped and projected public DTO query.
// Reject export, aggregate and arbitrary property paths on customer-facing routes.
public static class StorefrontPaging
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
                    ValidFilter<T>(f))) &&
               (filter.SortingParams == null || filter.SortingParams.All(s =>
                    s != null && allowed.Contains(s.ColumnName ?? "") && Enum.IsDefined(s.SortOrder))) &&
               Enum.IsDefined(filter.ComparisonMode);
    }

    private static bool ValidFilter<T>(FilterParams f)
    {
        var property = typeof(T).GetProperty(f.ColumnName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property == null || !Enum.IsDefined(f.FilterOption) || f.FilterOption is ComparisonMethod.FullTextContains or ComparisonMethod.FullTextFreeText) return false;
        var value = f.FilterValue;
        if (property.PropertyType == typeof(string))
            return f.FilterOption is ComparisonMethod.IsNullOrWhiteSpace or ComparisonMethod.IsNotNullOrWhiteSpace ||
                (!string.IsNullOrWhiteSpace(value) && value.Length <= 256 && (int)f.FilterOption >= 5);
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || (int)f.FilterOption > 6) return false;
        if (property.PropertyType == typeof(int) || property.PropertyType == typeof(int?)) return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        if (property.PropertyType == typeof(decimal)) return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _);
        if (property.PropertyType == typeof(DateTime)) return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _);
        return false;
    }
}
