using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using BonakMarket.Core.Common;
using BonakMarket.Identity.Models;
using BonakMarket.Identity.Queries;

namespace BonakMarket.Core.Tests.Platform.Identity;

[TestFixture]
public sealed class LocationQueriesTests
{
    [Test]
    public async Task Provinces_filter_before_paging_and_preserve_public_scope()
    {
        var provinces = new[]
        {
            new StateProvince { Id = 1, CountryId = 1, Name = "تهران", Published = true },
            new StateProvince { Id = 2, CountryId = 1, Name = "تهران غرب", Published = true },
            new StateProvince { Id = 3, CountryId = 1, Name = "تهران شرق", Published = false },
            new StateProvince { Id = 4, CountryId = 2, Name = "تهران", Published = true },
            new StateProvince { Id = 5, CountryId = 1, Name = "تهران شمال", Published = true }
        };
        var cities = provinces.Select(p => new City { Id = p.Id, StateProvinceId = p.Id, Published = p.Id != 5 });
        var request = new LocationListRequest { Search = " تهران ", Page = 2, PageSize = 1 };
        var query = LocationQueries.Provinces(provinces.AsQueryable(), cities.AsQueryable(), 1, request);
        var page = await query.ToPagedList(request.Page - 1, request.PageSize).LoadAsync();

        Assert.That(page.TotalCount, Is.EqualTo(2));
        Assert.That(page.TotalPages, Is.EqualTo(2));
        Assert.That(page.Single().Id, Is.EqualTo(2));
        request.Id = 1;
        Assert.That(LocationQueries.Provinces(provinces.AsQueryable(), cities.AsQueryable(), 1, request).Single().Id, Is.EqualTo(1));
    }

    [Test]
    public void Cities_filter_by_name_and_id_without_exposing_other_provinces_or_unpublished_cities()
    {
        var province = new StateProvince { Id = 1, CountryId = 1, Published = true };
        var other = new StateProvince { Id = 2, CountryId = 1, Published = true };
        var cities = new[]
        {
            new City { Id = 1, StateProvinceId = 1, StateProvince = province, Name = "کیان شهر", Published = true },
            new City { Id = 2, StateProvinceId = 1, StateProvince = province, Name = "کیان شهر", Published = false },
            new City { Id = 3, StateProvinceId = 2, StateProvince = other, Name = "کیان شهر", Published = true },
            new City { Id = 4, StateProvinceId = 1, StateProvince = province, Name = "تهران", Published = true }
        }.AsQueryable();
        var request = new LocationListRequest { Search = "كيان\u200cشهر" };

        Assert.That(LocationQueries.Cities(cities, 1, 1, request).Single().Id, Is.EqualTo(1));
        request.Id = 3;
        Assert.That(LocationQueries.Cities(cities, 1, 1, request), Is.Empty);
        request.Id = null;
        Assert.That(LocationQueries.Cities(cities, 2, 1, request), Is.Empty);
        province.Published = false;
        Assert.That(LocationQueries.Cities(cities, 1, 1, request), Is.Empty);
    }

    [TestCase(0, 20, null, null)]
    [TestCase(10001, 20, null, null)]
    [TestCase(1, 101, null, null)]
    [TestCase(1, 20, 0, null)]
    [TestCase(1, 20, null, "too-long")]
    public void Invalid_request_is_rejected(int page, int pageSize, int? id, string search)
    {
        var request = new LocationListRequest
        {
            Page = page, PageSize = pageSize, Id = id,
            Search = search == null ? null : new string('a', 201)
        };
        Assert.That(Validator.TryValidateObject(request, new ValidationContext(request),
            new List<ValidationResult>(), true), Is.False);
    }

    [Test]
    public void Sql_server_translates_search_scope_and_pagination()
    {
        using var db = new LocationSqlContext(new DbContextOptionsBuilder<LocationSqlContext>()
            .UseSqlServer("Server=localhost;Database=BonakMarketTranslationOnly;Integrated Security=True;TrustServerCertificate=True").Options);
        var request = new LocationListRequest { Id = 1, Search = "تهران", Page = 2, PageSize = 5 };
        var provinces = LocationQueries.Provinces(db.Set<StateProvince>(), db.Set<City>(), 1, request)
            .ApplyPaging(request.Page - 1, request.PageSize).ToQueryString();
        var cities = LocationQueries.Cities(db.Set<City>(), 1, 1, request)
            .ApplyPaging(request.Page - 1, request.PageSize).ToQueryString();

        foreach (var sql in new[] { provinces, cities })
        {
            Assert.That(sql, Does.Contain("REPLACE").IgnoreCase);
            Assert.That(sql, Does.Contain("Published").IgnoreCase);
            Assert.That(sql, Does.Contain("CountryId").IgnoreCase);
            Assert.That(sql, Does.Contain("ORDER BY").IgnoreCase);
            Assert.That(sql, Does.Contain("OFFSET").IgnoreCase);
        }
    }

    [Test]
    public void Response_metadata_matches_standard_page_contract()
    {
        var page = new LocationPage<AddressLocation>([], 2, 20, 21, 2);
        Assert.That(page.HasPreviousPage, Is.True);
        Assert.That(page.HasNextPage, Is.False);
    }
}
