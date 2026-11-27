using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using BonakMarket.Core.Catalog.Search;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Catalog.Attributes;
using BonakMarket.Core.Localization;
using BonakMarket.Core.Search;
using BonakMarket.StorefrontApi.Controllers;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class StorefrontSearchContractTests
{
    [TestCase(null, null, true)]
    [TestCase("rice", 1, true)]
    [TestCase("rice", 16, true)]
    [TestCase("rice", 0, false)]
    [TestCase("rice", 17, false)]
    public void ValidatesInstantSearchRequest(string search, int? limit, bool expected)
    {
        var request = new InstantSearchRequest { Search = search, Limit = limit };
        Assert.That(Validator.TryValidateObject(request, new ValidationContext(request),
            new List<ValidationResult>(), true), Is.EqualTo(expected));
    }

    [Test]
    public void RejectsOverlongTerm()
    {
        var request = new InstantSearchRequest { Search = new string('x', 201) };
        Assert.That(Validator.TryValidateObject(request, new ValidationContext(request),
            new List<ValidationResult>(), true), Is.False);
    }

    [TestCase(true, 10, null, 10, true)]
    [TestCase(true, 10, 3, 3, true)]
    [TestCase(true, 10, 16, 10, true)]
    [TestCase(true, 100, null, 16, true)]
    [TestCase(true, 0, null, 0, false)]
    [TestCase(false, 10, null, 10, false)]
    public void HonorsAdminLimitAndEnabledFlag(bool enabled, int configured, int? requested, int expectedLimit, bool expectedEnabled)
    {
        var result = CatalogFacade.InstantSearchPolicy(new InstantSearchRequest { Limit = requested },
            new SearchSettings { InstantSearchEnabled = enabled, InstantSearchNumberOfProducts = configured,
                InstantSearchTermMinLength = 3 });
        Assert.That(result.Limit, Is.EqualTo(expectedLimit));
        Assert.That(result.Enabled, Is.EqualTo(expectedEnabled));
        Assert.That(result.MinimumTermLength, Is.EqualTo(3));
    }

    [TestCase(nameof(CatalogController.Search), null)]
    [TestCase(nameof(CatalogController.InstantSearch), "suggestions")]
    [TestCase(nameof(CatalogController.Facets), "facets")]
    public void SearchRoutesArePublicCatalogGets(string methodName, string route)
    {
        var action = typeof(CatalogController).GetMethod(methodName);
        Assert.That(action.IsDefined(typeof(PublicCatalogAttribute), true), Is.True);
        Assert.That(typeof(CatalogController).GetCustomAttribute<RouteAttribute>().Template, Is.EqualTo("api/storefront/v1/products"));
        Assert.That(action.GetCustomAttributes<HttpGetAttribute>().Select(a => a.Template).ToArray(), Is.EqualTo(new[] { route }));
    }

    [Test]
    public void FacetsRequestContainsOnlySharedFilters()
    {
        Assert.That(typeof(ProductFacetsRequest).GetProperties().Select(x => x.Name), Is.EquivalentTo(
            new[] { "Search", "CategoryId", "BrandId", "MinPrice", "MaxPrice", "InStock", "HasDiscount", "ShowOnHomePage" }));
        var filter = new ProductFacetsRequest { Search = "rice", CategoryId = 2, BrandId = 3,
            MinPrice = 10, MaxPrice = 20, InStock = false, HasDiscount = true, ShowOnHomePage = true };
        var mapped = filter.ToSearchRequest();
        Assert.That(mapped.Search, Is.EqualTo(filter.Search));
        Assert.That(mapped.CategoryId, Is.EqualTo(filter.CategoryId));
        Assert.That(mapped.BrandId, Is.EqualTo(filter.BrandId));
        Assert.That(mapped.MinPrice, Is.EqualTo(filter.MinPrice));
        Assert.That(mapped.MaxPrice, Is.EqualTo(filter.MaxPrice));
        Assert.That(mapped.InStock, Is.EqualTo(filter.InStock));
        Assert.That(mapped.HasDiscount, Is.EqualTo(filter.HasDiscount));
        Assert.That(mapped.ShowOnHomePage, Is.EqualTo(filter.ShowOnHomePage));
        Assert.That(mapped.IncludePrice, Is.False);
        Assert.That(mapped.Sort, Is.EqualTo(CatalogSort.Initial));
    }

    [TestCase(-1, 20, false)]
    [TestCase(20, 10, false)]
    [TestCase(10, 20, true)]
    public void FacetsValidatesPriceRange(int min, int max, bool expected)
    {
        var filter = new ProductFacetsRequest { MinPrice = min, MaxPrice = max };
        Assert.That(Validator.TryValidateObject(filter, new ValidationContext(filter),
            new List<ValidationResult>(), true), Is.EqualTo(expected));
    }

    [TestCase(null, true)]
    [TestCase("   ", true)]
    [TestCase("a", true)]
    [TestCase("rice", false)]
    public async Task ShortOrDisabledSearchReturnsEmptyWithoutQueryingDatabase(string term, bool enabled)
    {
        // All dependencies are deliberately null: a rejected/disabled search must not touch them.
        var facade = new CatalogFacade(null, null, null, null, null, null, null, null, null, null, null, null);
        var result = await facade.InstantSearchAsync(new InstantSearchRequest { Search = term },
            new SearchSettings { InstantSearchEnabled = enabled, InstantSearchTermMinLength = 2 },
            CancellationToken.None);
        Assert.That(result.Items, Is.Empty);
        Assert.That(result.HasMore, Is.False);
        Assert.That(result.Enabled, Is.EqualTo(enabled));
        Assert.That(result.Search, Is.EqualTo(term?.Trim() ?? string.Empty));
    }

    [Test]
    public void UsesOnlyNativeConfiguredSearchFields()
    {
        var settings = new SearchSettings { SearchFields = new List<string> { "sku", "category", "name", "sku" } };
        Assert.That(CatalogFacade.SearchFields(settings), Is.EquivalentTo(new[] { "name", "sku" }));
    }

    [TestCase("rice", false, null, true)]
    [TestCase("rice", true, null, null)]
    [TestCase("", false, null, null)]
    [TestCase("rice", false, false, false)]
    [TestCase("rice", true, true, true)]
    public void AvailabilityDefaultsAndOverridesAreConsistent(string term, bool includeUnavailable,
        bool? requested, bool? expected)
    {
        Assert.That(CatalogFacade.EffectiveStockFilter(new ProductSearchRequest { Search = term, InStock = requested },
            new SearchSettings { IncludeNotAvailable = includeUnavailable }), Is.EqualTo(expected));
    }

    [Test]
    public void FalseStockFilterSelectsOnlyUnavailableProducts()
    {
        var products = new[]
        {
            new Product { Id = 1, ManageInventoryMethodId = (int)ManageInventoryMethod.ManageStock,
                StockQuantity = 0, BackorderModeId = (int)BackorderMode.NoBackorders },
            new Product { Id = 2, ManageInventoryMethodId = (int)ManageInventoryMethod.ManageStock,
                StockQuantity = 2, BackorderModeId = (int)BackorderMode.NoBackorders },
            new Product { Id = 3, ManageInventoryMethodId = (int)ManageInventoryMethod.DontManageStock }
        }.AsQueryable();
        Assert.That(CatalogFacade.ApplyStockFilter(products, false).Select(x => x.Id), Is.EqualTo(new[] { 1 }));
        Assert.That(CatalogFacade.ApplyStockFilter(products, true).Select(x => x.Id), Is.EqualTo(new[] { 2, 3 }));
        Assert.That(CatalogFacade.ApplyStockFilter(products, null).Count(), Is.EqualTo(3));
    }

    [Test]
    public void CatalogPriceFilterIsInclusiveAndUsesOnlyActiveSpecialPrice()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var products = new[]
        {
            new Product { Id = 1, Price = 100, SpecialPrice = 50, SpecialPriceStartDateTimeUtc = now },
            new Product { Id = 2, Price = 50, SpecialPrice = 20, SpecialPriceEndDateTimeUtc = now.AddDays(-1) },
            new Product { Id = 3, Price = 100, SpecialPrice = 50, SpecialPriceStartDateTimeUtc = now.AddDays(1) }
        }.AsQueryable();
        var filter = new ProductSearchRequest { MinPrice = 50, MaxPrice = 50, Sort = CatalogSort.PriceDesc };
        Assert.That(CatalogFacade.ApplyCatalogPriceAndSort(products, filter, now).Select(x => x.Id),
            Is.EqualTo(new[] { 1, 2 }));
    }

    [TestCase(CatalogSort.NameAsc)]
    [TestCase(CatalogSort.NameDesc)]
    [TestCase(CatalogSort.PriceAsc)]
    [TestCase(CatalogSort.PriceDesc)]
    [TestCase(CatalogSort.Newest)]
    public void ExplicitSortUsesIdTieBreaker(CatalogSort sort)
    {
        var products = new[]
        {
            new Product { Id = 3, Name = "same", Price = 10 },
            new Product { Id = 1, Name = "same", Price = 10 },
            new Product { Id = 2, Name = "same", Price = 10 }
        }.AsQueryable();
        Assert.That(CatalogFacade.ApplyCatalogPriceAndSort(products, new ProductSearchRequest { Sort = sort },
            DateTime.UtcNow).Select(x => x.Id), Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void InstantProductContractDoesNotExposePrice()
    {
        Assert.That(typeof(InstantSearchProductDto).GetProperties().Select(x => x.Name),
            Is.EquivalentTo(new[] { "Id", "Name", "Sku", "MediaFileId", "ImageUrl" }));
    }

    [TestCase("شير كم‌چرب", "شیر کم چرب", SearchMode.ExactMatch, true)]
    [TestCase("شير کم", "شیر کم‌چرب", SearchMode.StartsWith, true)]
    [TestCase("کم چرب", "شیر کم‌چرب", SearchMode.Contains, true)]
    [TestCase("کم چرب", "شیر کم‌چرب", SearchMode.ExactMatch, false)]
    public void PersianSimpleSearchNormalizesStoredAndInputText(string term, string name, SearchMode mode, bool expected)
    {
        var rows = new[] { new Product { Id = 1, Name = name } }.AsQueryable();
        var result = CatalogText.ApplyNormalizedSearch(rows, Array.Empty<LocalizedProperty>().AsQueryable(),
            term, new[] { "name" }, mode, 1);
        Assert.That(result.Any(), Is.EqualTo(expected));
    }

    [Test]
    public void PersianSearchUsesOnlyCurrentLanguageAndConfiguredFields()
    {
        var rows = new[] { new Product { Id = 1, Name = "milk" }, new Product { Id = 2, Name = "water" } }.AsQueryable();
        var translations = new[]
        {
            new LocalizedProperty { EntityId = 1, LanguageId = 2, LocaleKeyGroup = nameof(Product), LocaleKey = "Name", LocaleValue = "شير" },
            new LocalizedProperty { EntityId = 2, LanguageId = 3, LocaleKeyGroup = nameof(Product), LocaleKey = "Name", LocaleValue = "شیر" },
            new LocalizedProperty { EntityId = 2, LanguageId = 2, LocaleKeyGroup = nameof(Product), LocaleKey = "ShortDescription", LocaleValue = "شیر" }
        }.AsQueryable();
        Assert.That(CatalogText.ApplyNormalizedSearch(rows, translations, "شیر", new[] { "name" }, SearchMode.Contains, 2)
            .Select(x => x.Id), Is.EqualTo(new[] { 1 }));
        Assert.That(CatalogText.UseNormalizedSearch("شیر"), Is.True);
        Assert.That(CatalogText.UseNormalizedSearch("شیر + کم"), Is.False);
        Assert.That(CatalogText.UseNormalizedSearch("rice"), Is.False);
    }

    [Test]
    public void InactiveVariantStockDoesNotMakeAProductSellable()
    {
        var inactive = new Product { Id = 1, ManageInventoryMethodId = (int)ManageInventoryMethod.ManageStockByAttributes };
        inactive.ProductVariantAttributeCombinations.Add(new ProductVariantAttributeCombination { IsActive = false, StockQuantity = 10 });
        var active = new Product { Id = 2, ManageInventoryMethodId = (int)ManageInventoryMethod.ManageStockByAttributes };
        active.ProductVariantAttributeCombinations.Add(new ProductVariantAttributeCombination { IsActive = true, StockQuantity = 1 });
        var products = new[] { inactive, active }.AsQueryable();
        Assert.That(CatalogFacade.ApplyStockFilter(products, true).Select(x => x.Id), Is.EqualTo(new[] { 2 }));
        Assert.That(CatalogFacade.ApplyStockFilter(products, false).Select(x => x.Id), Is.EqualTo(new[] { 1 }));
    }

    [TestCase(CatalogSort.PriceAsc)]
    [TestCase(CatalogSort.PriceDesc)]
    public void PriceSortRetainsUnpricedProductsWithoutExposingTheirPrices(CatalogSort sort)
    {
        var products = new[]
        {
            new Product { Id = 1, Price = 10, CallForPrice = true },
            new Product { Id = 2, Price = 100 },
            new Product { Id = 3, Price = 50, CustomerEntersPrice = true }
        }.AsQueryable();
        Assert.That(CatalogFacade.ApplyCatalogPriceAndSort(products, new ProductSearchRequest { Sort = sort }, DateTime.UtcNow)
            .Select(x => x.Id), Is.EqualTo(new[] { 2, 1, 3 }));
        Assert.That(CatalogFacade.ApplyCatalogPriceAndSort(products, new ProductSearchRequest { MaxPrice = 200 }, DateTime.UtcNow)
            .Select(x => x.Id), Is.EqualTo(new[] { 2 }));
    }
}
