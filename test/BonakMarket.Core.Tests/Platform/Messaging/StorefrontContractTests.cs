using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Moq;
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
using BonakMarket.MultiSms.Controllers;
using BonakMarket.MultiSms.Security;
using Microsoft.AspNetCore.Http;

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

    [TestCase("/api/sms-login/profile", true)]
    [TestCase("/api/sms-login/profile/phone/verify", true)]
    [TestCase("/api/sms-login/session", true)]
    [TestCase("/api/sms-login/logout", true)]
    [TestCase("/api/sms-login/refresh", false)]
    [TestCase("/api/sms-login/REQUEST/", false)]
    [TestCase("/api/sms-login/verify", false)]
    [TestCase("/api/storefront/v1/cart", true)]
    [TestCase("/api/storefront/v10/cart", false)]
    [TestCase("/odata/v1/Products", false)]
    [TestCase("/api/sms-gateway/test", false)]
    [TestCase("/login", false)]
    public void BearerIsRestrictedToCustomerApi(string path, bool expected)
    {
        Assert.That(CustomerApiBearerMiddleware.UsesCustomerBearer(new PathString(path)), Is.EqualTo(expected));
    }

    [Test]
    public async Task InvalidBearerCannotFallBackToCookieOnCustomerApi()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Scheme = "https";
        http.Request.Path = "/api/sms-login/profile";
        http.Request.Headers.Authorization = "Bearer invalid-token";
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "7") }, "cookie"));
        using var body = new MemoryStream();
        http.Response.Body = body;
        bool called = false;
        var middleware = new CustomerApiBearerMiddleware(_ => { called = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(http);

        Assert.That(called, Is.False);
        Assert.That(http.Response.StatusCode, Is.EqualTo(401));
        Assert.That(http.Items.ContainsKey(typeof(IMobileApiTokenService)), Is.False);
    }

    [Test]
    public async Task ValidBearerInstallsOnlyVerifiedCustomerIdentity()
    {
        var customer = new Customer { Id = 123 };
        var tokens = new Mock<IMobileApiTokenService>();
        tokens.SetupGet(x => x.IsEnabled).Returns(true);
        tokens.Setup(x => x.ValidateAsync("valid-token", It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        using var services = new ServiceCollection().AddSingleton(tokens.Object).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Scheme = "https";
        http.Request.Path = "/api/storefront/v1/orders";
        http.Request.Headers.Authorization = "Bearer valid-token";
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "7") }, "cookie"));
        bool called = false;
        var middleware = new CustomerApiBearerMiddleware(_ => { called = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(http);

        Assert.That(called, Is.True);
        Assert.That(http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, Is.EqualTo("123"));
        Assert.That(http.Items[typeof(IMobileApiTokenService)], Is.SameAs(customer));
        tokens.Verify(x => x.ValidateAsync("valid-token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase("/odata/v1/Countries")]
    [TestCase("/api/sms-login/refresh")]
    public async Task OtherAuthenticationFlowsAreNotIntercepted(string path)
    {
        var http = new DefaultHttpContext();
        http.Request.Path = path;
        http.Request.Headers.Authorization = "Basic integration";
        var principal = http.User;
        bool called = false;
        var middleware = new CustomerApiBearerMiddleware(_ => { called = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(http);

        Assert.That(called, Is.True);
        Assert.That(http.User, Is.SameAs(principal));
        Assert.That(http.Items.ContainsKey(typeof(IMobileApiTokenService)), Is.False);
    }

    [Test]
    public void AccountRoutesAreOwnedByMultiSms()
    {
        var type = typeof(SmsLoginApiController);
        Assert.That(type.GetCustomAttribute<RouteAttribute>()?.Template, Is.EqualTo("api/sms-login"));
        Assert.That(type.GetMethod(nameof(SmsLoginApiController.GetProfile))?.GetCustomAttribute<HttpGetAttribute>()?.Template, Is.EqualTo("profile"));
        Assert.That(type.GetMethod(nameof(SmsLoginApiController.UpdateProfile))?.GetCustomAttribute<HttpPutAttribute>()?.Template, Is.EqualTo("profile"));
        Assert.That(type.GetMethod(nameof(SmsLoginApiController.Logout))?.GetCustomAttribute<HttpPostAttribute>()?.Template, Is.EqualTo("logout"));
        Assert.That(typeof(StorefrontController).Assembly.GetType("BonakMarket.StorefrontApi.Controllers.ProfileController"), Is.Null);
        Assert.That(typeof(StorefrontController).Assembly.GetType("BonakMarket.StorefrontApi.Controllers.AuthController"), Is.Null);
    }

    [Test]
    public void PrivateControllersDoNotMarkActionsPublic()
    {
        var types = new[] { typeof(OrdersController), typeof(CartController) };
        foreach (var type in types)
        {
            Assert.That(type.GetCustomAttributes<TypeFilterAttribute>(true).Any(x => x.ImplementationType == typeof(StorefrontAuthorizationFilter)), Is.True);
            Assert.That(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Any(m => m.IsDefined(typeof(PublicCatalogAttribute), true)), Is.False);
        }
    }
}
