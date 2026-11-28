using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using BonakMarket.Core.Common;
using BonakMarket.Identity.Models;

namespace BonakMarket.Identity.Queries;

/// <summary>Composes public location queries before native paging.</summary>
public static class LocationQueries
{
    /// <summary>Returns published provinces with at least one selectable city.</summary>
    public static IQueryable<StateProvince> Provinces(IQueryable<StateProvince> provinces, IQueryable<City> cities,
        int countryId, LocationListRequest request)
    {
        var query = provinces.Where(p => p.CountryId == countryId && p.Published &&
            cities.Any(c => c.StateProvinceId == p.Id && c.Published));
        if (request.Id.HasValue)
            query = query.Where(p => p.Id == request.Id.Value);

        return WhereNameContains(query, request.Search, p => p.Name)
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Name).ThenBy(p => p.Id);
    }

    /// <summary>Returns published cities in the requested published Iranian province.</summary>
    public static IQueryable<City> Cities(IQueryable<City> cities, int countryId, int provinceId, LocationListRequest request)
    {
        var query = cities.Where(c => c.StateProvinceId == provinceId && c.Published &&
            c.StateProvince.Published && c.StateProvince.CountryId == countryId);
        if (request.Id.HasValue)
            query = query.Where(c => c.Id == request.Id.Value);

        return WhereNameContains(query, request.Search, c => c.Name)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ThenBy(c => c.Id);
    }

    private static IQueryable<T> WhereNameContains<T>(IQueryable<T> query, string search, Expression<Func<T, string>> name)
    {
        var term = (search ?? string.Empty).Trim()
            .Replace("ي", "ی").Replace("ك", "ک").Replace("\u200c", "").Replace(" ", "");
        if (term.Length == 0)
            return query;

        Expression<Func<string, bool>> match = value => (value ?? "").Replace("ي", "ی").Replace("ك", "ک")
            .Replace("\u200c", "").Replace(" ", "").Contains(term);
        var body = ReplacingExpressionVisitor.Replace(match.Parameters[0], name.Body, match.Body);
        return query.Where(Expression.Lambda<Func<T, bool>>(body, name.Parameters));
    }
}
