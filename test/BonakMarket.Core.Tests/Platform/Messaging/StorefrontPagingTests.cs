using System.Collections.Generic;
using Common.Paging;
using Common.Paging.Enums;
using NUnit.Framework;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class StorefrontPagingTests
{
    [Test]
    public void ValidPageAndFieldAreAccepted()
    {
        var filter = new PagingFilter { PageNumber = 1, PageSize = 20,
            FilterParam = new List<FilterParams> { new() { ColumnName = "Name", FilterOption = ComparisonMethod.Contain, FilterValue = "milk" } } };
        Assert.That(StorefrontPaging.Validate<ProductDto>(filter, "Id", "Name"), Is.True);
    }
    [TestCase("CustomerId")]
    [TestCase("Customer.Password")]
    [TestCase("ProductCost")]
    public void HiddenFieldsAreRejected(string field)
    {
        var filter = new PagingFilter { FilterParam = new List<FilterParams> { new() { ColumnName = field, FilterOption = ComparisonMethod.Equal, FilterValue = "1" } } };
        Assert.That(StorefrontPaging.Validate<OrderDto>(filter, "Id", "OrderTotal"), Is.False);
    }
    [TestCase(-1)]
    [TestCase(101)]
    public void UnboundedPageIsRejected(int size)
    {
        Assert.That(StorefrontPaging.Validate<ProductDto>(new PagingFilter { PageSize = size }, "Id"), Is.False);
    }
    [Test]
    public void ExcelExportAndFullTextAreRejected()
    {
        Assert.That(StorefrontPaging.Validate<ProductDto>(new PagingFilter { OutputType = OutputType.Excel }, "Id"), Is.False);
        var filter = new PagingFilter { FilterParam = new List<FilterParams> { new() { ColumnName = "Name", FilterOption = ComparisonMethod.FullTextContains, FilterValue = "milk" } } };
        Assert.That(StorefrontPaging.Validate<ProductDto>(filter, "Name"), Is.False);
    }
    [Test]
    public void InvalidNumericFilterIsRejected()
    {
        var filter = new PagingFilter { FilterParam = new List<FilterParams> { new() { ColumnName = "Id", FilterOption = ComparisonMethod.Equal, FilterValue = "not a number" } } };
        Assert.That(StorefrontPaging.Validate<ProductDto>(filter, "Id"), Is.False);
    }
}
