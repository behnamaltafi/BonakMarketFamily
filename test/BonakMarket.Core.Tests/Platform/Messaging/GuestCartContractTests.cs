using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using BonakMarket.Core;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Security;
using BonakMarket.Core.Stores;
using BonakMarket.StorefrontApi;
using BonakMarket.StorefrontApi.Controllers;
using BonakMarket.StorefrontApi.Domain;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public sealed class GuestCartContractTests
{
    [Test]
    public void TokenIsOpaqueAndHashIsStable()
    {
        var token = GuestCartPolicy.Generate();
        Assert.That(token.Length, Is.EqualTo(43));
        Assert.That(GuestCartPolicy.Hash(token).Length, Is.EqualTo(64));
        Assert.That(GuestCartPolicy.Hash(token), Is.EqualTo(GuestCartPolicy.Hash(token)));
        Assert.That(GuestCartPolicy.Generate(), Is.Not.EqualTo(token));
        Assert.That(GuestCartPolicy.Hash("123"), Is.Null);
        Assert.That(GuestCartPolicy.Hash(new string('!', 43)), Is.Null);
        var request = new DefaultHttpContext().Request;
        request.Headers[GuestCartPolicy.Header] = new Microsoft.Extensions.Primitives.StringValues(new[] { token, token });
        Assert.That(GuestCartPolicy.ReadHash(request), Is.Null);
    }

    [Test]
    public void GuestTokenCannotRepresentAnUpgradedOrPrivilegedCustomer()
    {
        var guest = new Customer { Id = 123, Active = true };
        guest.CustomerRoleMappings.Add(new CustomerRoleMapping
        { CustomerRole = new CustomerRole { Active = true, SystemName = SystemCustomerRoleNames.Guests } });
        Assert.That(GuestCartPolicy.IsGuest(guest), Is.True);
        guest.Email = "registered@example.invalid";
        Assert.That(GuestCartPolicy.IsGuest(guest), Is.False);
        guest.Email = null;
        guest.CustomerRoleMappings.Add(new CustomerRoleMapping
        { CustomerRole = new CustomerRole { Active = false, SystemName = SystemCustomerRoleNames.Registered } });
        Assert.That(GuestCartPolicy.IsGuest(guest), Is.False);
    }

    [Test]
    public void GuestAccessIsLimitedToExplicitCartMethods()
    {
        var methods = typeof(CartController).GetMethods().Where(m => m.IsDefined(typeof(AllowGuestCartAttribute), true)).Select(m => m.Name);
        Assert.That(methods, Is.EquivalentTo(new[] { "Get", "Items", "Add", "Update", "Delete", "Total", "ApplyCoupon", "RemoveCoupon" }));
        Assert.That(typeof(CartController).GetMethod("CreateGuest").IsDefined(typeof(CreateGuestCartAttribute), true), Is.True);
        Assert.That(typeof(CartController).GetMethod("Merge").IsDefined(typeof(MergeGuestCartAttribute), true), Is.True);
        foreach (var type in new[] { typeof(CheckoutController), typeof(CheckoutSessionsController), typeof(WishlistController), typeof(OrdersController) })
            Assert.That(type.GetMethods().Any(m => m.IsDefined(typeof(AllowGuestCartAttribute), true)), Is.False);
    }

    [TestCase("Get", 123, 1, true, 200)]
    [TestCase("Get", 999, 1, true, 403)]
    [TestCase("Get", 123, 2, true, 403)]
    [TestCase("Get", 123, 1, false, 401)]
    [TestCase("ApplyGiftCard", 123, 1, true, 401)]
    [TestCase("ToWishlist", 123, 1, true, 401)]
    [TestCase("Merge", 123, 1, true, 401)]
    public async Task AuthorizationRequiresVerifiedGuestScopeAndDoesNotGrantPrivateActions(string method, int tokenCustomer, int tokenStore, bool hasToken, int status)
    {
        var customer = new Customer { Id = 123, Active = true };
        customer.CustomerRoleMappings.Add(new CustomerRoleMapping
        { CustomerRole = new CustomerRole { Active = true, SystemName = SystemCustomerRoleNames.Guests } });
        var work = new Mock<IWorkContext>();
        work.SetupGet(x => x.CurrentCustomer).Returns(customer);
        var stores = new Mock<IStoreContext>();
        stores.SetupGet(x => x.CurrentStore).Returns(new Store { Id = 1 });
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(x => x.AuthorizeAsync(It.IsAny<string>(), customer, false)).ReturnsAsync(true);
        var request = new StorefrontRequestContext();
        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        if (hasToken)
            http.Items[typeof(GuestCartToken)] = new GuestCartToken { CustomerId = tokenCustomer, StoreId = tokenStore };
        var type = typeof(CartController);
        var descriptor = new ControllerActionDescriptor { ControllerTypeInfo = type.GetTypeInfo(), MethodInfo = type.GetMethod(method) };
        var context = new AuthorizationFilterContext(new ActionContext(http, new RouteData(), descriptor), new List<IFilterMetadata>());
        await new StorefrontAuthorizationFilter(work.Object, stores.Object, permissions.Object, request).OnAuthorizationAsync(context);
        if (status == 200)
        {
            Assert.That(context.Result, Is.Null);
            Assert.That(request.IsGuestCart, Is.True);
            permissions.Verify(x => x.AuthorizeAsync(StorefrontPermissions.GuestCart, customer, false), Times.Once);
            permissions.Verify(x => x.AuthorizeAsync(StorefrontPermissions.Cart, customer, false), Times.Never);
        }
        else Assert.That(((ObjectResult)context.Result).StatusCode, Is.EqualTo(status));
    }

    [TestCase(false, 401)]
    [TestCase(true, 401)]
    public async Task MissingOrMalformedTokenFailsBeforeNativeWorkContext(bool malformed, int status)
    {
        using var services = new ServiceCollection().AddSingleton<IConfiguration>(new ConfigurationBuilder().Build()).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Scheme = "https";
        http.Request.Path = "/api/storefront/v1/cart";
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new AllowGuestCartAttribute()), "cart"));
        if (malformed) http.Request.Headers[GuestCartPolicy.Header] = "not-a-token";
        using var body = new MemoryStream();
        http.Response.Body = body;
        var called = false;
        await new GuestCartMiddleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(http);
        Assert.That(called, Is.False);
        Assert.That(http.Response.StatusCode, Is.EqualTo(status));
        Assert.That(http.User.Identity?.IsAuthenticated, Is.False);
    }
}
