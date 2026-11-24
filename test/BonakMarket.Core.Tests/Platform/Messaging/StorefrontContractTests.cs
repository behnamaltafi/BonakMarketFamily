using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using BonakMarket.Core.Identity;
using BonakMarket.StorefrontApi;
using BonakMarket.StorefrontApi.Controllers;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class StorefrontContractTests
{
    private static bool IsValid(object value) =>
        Validator.TryValidateObject(value, new ValidationContext(value), new List<ValidationResult>(), true);

    [TestCase(1, 1, true)]
    [TestCase(0, 20, false)]
    [TestCase(1, 101, false)]
    [TestCase(10001, 20, false)]
    public void PageBoundsAreValidated(int page, int size, bool expected)
    {
        Assert.That(IsValid(new ProductSearchRequest { Page = page, PageSize = size }), Is.EqualTo(expected));
    }

    [Test]
    public void InvalidPriceRangeAndSortAreRejected()
    {
        Assert.That(IsValid(new ProductSearchRequest { MinPrice = 100, MaxPrice = 50 }), Is.False);
        Assert.That(IsValid(new ProductSearchRequest { Sort = (CatalogSort)999 }), Is.False);
    }

    [Test]
    public async Task NativePagingPreservesRequestedPageAndCount()
    {
        var source = Enumerable.Range(1, 10).AsQueryable().OrderBy(x => x);
        var result = await source.PageAsync(new PageRequest { Page = 2, PageSize = 3 }, CancellationToken.None);
        Assert.That(result.Items, Is.EqualTo(new[] { 4, 5, 6 }));
        Assert.That(result.TotalCount, Is.EqualTo(10));
        Assert.That(result.TotalPages, Is.EqualTo(4));
        Assert.That(result.Page, Is.EqualTo(2));
    }

    [Test]
    public void GuestRoleGetsCatalogButNoPrivateCapabilities()
    {
        var defaults = new StorefrontPermissionProvider().GetDefaultPermissions().ToList();
        var guests = defaults.Single(x => x.CustomerRoleSystemName == SystemCustomerRoleNames.Guests);
        Assert.That(guests.PermissionRecords.Select(x => x.SystemName), Is.EquivalentTo(new[] { StorefrontPermissions.Catalog }));
        var registered = defaults.Single(x => x.CustomerRoleSystemName == SystemCustomerRoleNames.Registered);
        Assert.That(registered.PermissionRecords.Select(x => x.SystemName), Does.Contain(StorefrontPermissions.Orders));
    }

    [Test]
    public void PrivateControllersDoNotMarkActionsPublic()
    {
        var types = new[] { typeof(OrdersController), typeof(CartController), typeof(ProfileController), typeof(AuthController) };
        foreach (var type in types)
        {
            Assert.That(type.GetCustomAttributes<TypeFilterAttribute>(true).Any(x => x.ImplementationType == typeof(StorefrontAuthorizationFilter)), Is.True);
            Assert.That(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Any(m => m.IsDefined(typeof(PublicCatalogAttribute), true)), Is.False);
        }
    }
}
