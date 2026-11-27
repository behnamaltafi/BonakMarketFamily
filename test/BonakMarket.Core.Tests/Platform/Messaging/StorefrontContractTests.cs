using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
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
using BonakMarket.Identity.Controllers;
using BonakMarket.Identity.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using BonakMarket.Core;
using BonakMarket.Core.Security;
using BonakMarket.Core.Stores;

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
    public async Task NativeMappedPagingPreservesMetadataAndOnlyMapsPageRows()
    {
        const int page = 2;
        const int size = 3;
        var expected = new[] { "4", "5", "6" };
        var source = Enumerable.Range(1, 10).AsQueryable().OrderBy(x => x);
        var mappedCount = -1;
        var result = await source.PageAsync(new PageRequest { Page = page, PageSize = size }, items =>
        {
            mappedCount = items.Count;
            return Task.FromResult(items.Select(x => x.ToString()).ToList());
        }, CancellationToken.None);
        Assert.That(mappedCount, Is.EqualTo(expected.Length));
        Assert.That(result.Items, Is.EqualTo(expected));
        Assert.That(result.TotalCount, Is.EqualTo(10));
        Assert.That(result.TotalPages, Is.EqualTo(4));
        Assert.That(result.Page, Is.EqualTo(page));
    }

    [Test]
    public void GuestRoleGetsCatalogAndScopedCartButNoPrivateCapabilities()
    {
        var defaults = new StorefrontPermissionProvider().GetDefaultPermissions().ToList();
        var guests = defaults.Single(x => x.CustomerRoleSystemName == SystemCustomerRoleNames.Guests);
        Assert.That(guests.PermissionRecords.Select(x => x.SystemName), Is.EquivalentTo(new[] { StorefrontPermissions.Catalog, StorefrontPermissions.GuestCart }));
        var registered = defaults.Single(x => x.CustomerRoleSystemName == SystemCustomerRoleNames.Registered);
        foreach (var permission in new[] { StorefrontPermissions.Orders, StorefrontPermissions.Wishlist,
            StorefrontPermissions.RewardPoints, StorefrontPermissions.StockSubscriptions, StorefrontPermissions.Checkout,
            StorefrontPermissions.Returns, StorefrontPermissions.Downloads, StorefrontPermissions.Reviews, StorefrontPermissions.Sharing })
            Assert.That(registered.PermissionRecords.Select(x => x.SystemName), Does.Contain(permission));
    }

    [TestCase("/api/identity/v1/me/profile", true)]
    [TestCase("/api/identity/v1/me/addresses", true)]
    [TestCase("/api/identity/v1/me/security/phone/verify", true)]
    [TestCase("/api/identity/v1/auth/session", false)]
    [TestCase("/api/identity/v1/auth/logout", true)]
    [TestCase("/api/identity/v1/auth/refresh", false)]
    [TestCase("/api/identity/v1/auth/sms/request", false)]
    [TestCase("/api/identity/v1/auth/sms/verify", false)]
    [TestCase("/api/identity/v1/auth/mobile/sms/request", false)]
    [TestCase("/api/identity/v1/auth/mobile/sms/verify", false)]
    [TestCase("/api/identity/v10/me/profile", false)]
    [TestCase("/api/sms-login/profile", false)]
    [TestCase("/api/sms-login/profile/phone/verify", false)]
    [TestCase("/api/sms-login/session", false)]
    [TestCase("/api/sms-login/logout", false)]
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
        http.Request.Path = "/api/identity/v1/me/profile";
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

    [TestCase("https", null, true)]
    [TestCase("http", null, false)]
    [TestCase("http", "true", false)]
    [TestCase("http", "invalid", false)]
    [TestCase("http", "false", true)]
    [TestCase("https", "false", true)]
    public async Task ValidBearerInstallsOnlyVerifiedCustomerIdentity(string scheme, string requireHttps, bool allowed)
    {
        var customer = new Customer { Id = 123 };
        var tokens = new Mock<IMobileApiTokenService>();
        tokens.SetupGet(x => x.IsEnabled).Returns(true);
        tokens.Setup(x => x.ValidateAsync("valid-token", It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Identity:RequireHttps"] = requireHttps
        }).Build();
        using var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration)
            .AddSingleton(tokens.Object).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Scheme = scheme;
        using var body = new MemoryStream();
        http.Response.Body = body;
        http.Request.Path = "/api/storefront/v1/orders";
        http.Request.Headers.Authorization = "Bearer valid-token";
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "7") }, "cookie"));
        bool called = false;
        var middleware = new CustomerApiBearerMiddleware(_ => { called = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(http);

        Assert.That(called, Is.EqualTo(allowed));
        if (!allowed)
        {
            Assert.That(http.Response.StatusCode, Is.EqualTo(401));
            Assert.That(http.Items.ContainsKey(typeof(IMobileApiTokenService)), Is.False);
            tokens.Verify(x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            return;
        }
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

    [TestCase("/api/storefront/v1/products")]
    [TestCase("/api/storefront/v1/cart")]
    [TestCase("/api/identity/v1/me/profile")]
    [TestCase("/api/identity/v1/auth/sms/request")]
    [TestCase("/api/identity/v1/auth/refresh")]
    [TestCase("/api/identity/v1/auth/logout")]
    public async Task CookieIdentityIsClearedBeforeCustomerWorkContext(string path)
    {
        var http = new DefaultHttpContext();
        http.Request.Path = path;
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "7") }, "cookie"));
        var ticket = new AuthenticationTicket(http.User, "cookie");
        http.Features.Set<IAuthenticateResultFeature>(new TestAuthenticationFeature
            { AuthenticateResult = AuthenticateResult.Success(ticket) });
        bool called = false;
        await new CustomerApiBearerMiddleware(ctx =>
        {
            called = true;
            Assert.That(ctx.User.Identity.IsAuthenticated, Is.False);
            Assert.That(ctx.Features.Get<IAuthenticateResultFeature>().AuthenticateResult.None, Is.True);
            return Task.CompletedTask;
        }).InvokeAsync(http);
        Assert.That(called, Is.True);
    }

    [TestCase(false, false, 401)]
    [TestCase(false, true, 200)]
    [TestCase(true, false, 200)]
    [TestCase(true, true, 200)]
    public async Task StorefrontAuthorizationRequiresVerifiedBearerForPrivateActions(bool bearer, bool publicAction, int status)
    {
        var customer = new Customer { Id = 123, Active = true };
        customer.CustomerRoleMappings.Add(new CustomerRoleMapping
        {
            CustomerRole = new CustomerRole { Active = true, SystemName = bearer ? SystemCustomerRoleNames.Registered : SystemCustomerRoleNames.Guests }
        });
        var work = new Mock<IWorkContext>();
        work.SetupGet(x => x.CurrentCustomer).Returns(customer);
        var stores = new Mock<IStoreContext>();
        stores.SetupGet(x => x.CurrentStore).Returns(new Store { Id = 1 });
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(x => x.AuthorizeAsync(It.IsAny<string>(), customer, false)).ReturnsAsync(true);
        var request = new StorefrontRequestContext();
        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        // A cookie principal alone is not evidence of authenticated API identity.
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "7") }, "cookie"));
        if (bearer)
        {
            http.Request.Headers.Authorization = "Bearer verified";
            http.Items[typeof(IMobileApiTokenService)] = customer;
        }
        var type = publicAction ? typeof(CatalogController) : typeof(CartController);
        var method = publicAction ? type.GetMethod(nameof(CatalogController.Search)) : type.GetMethods()
            .First(x => x.IsDefined(typeof(HttpGetAttribute), true));
        var action = new ControllerActionDescriptor { ControllerTypeInfo = type.GetTypeInfo(), MethodInfo = method };
        var filterContext = new AuthorizationFilterContext(new ActionContext(http, new RouteData(), action), new List<IFilterMetadata>());
        await new StorefrontAuthorizationFilter(work.Object, stores.Object, permissions.Object, request).OnAuthorizationAsync(filterContext);
        if (status == 200)
        {
            Assert.That(filterContext.Result, Is.Null);
            Assert.That(request.Customer, Is.SameAs(customer));
            Assert.That(request.StoreId, Is.EqualTo(1));
        }
        else
        {
            Assert.That(((ObjectResult)filterContext.Result).StatusCode, Is.EqualTo(status));
            Assert.That(request.Customer, Is.Null);
        }
    }

    private sealed class TestAuthenticationFeature : IAuthenticateResultFeature
    {
        public AuthenticateResult AuthenticateResult { get; set; }
    }

    [Test]
    public void AccountRoutesAreOwnedByIdentity()
    {
        var type = typeof(SmsLoginApiController);
        Assert.That(type.Assembly.GetName().Name, Is.EqualTo("BonakMarket.Identity"));
        Assert.That(typeof(CustomerApiBearerMiddleware).Assembly, Is.SameAs(type.Assembly));
        Assert.That(type.Assembly.GetReferencedAssemblies().Any(x => x.Name == "BonakMarket.MultiSms"), Is.False);
        Assert.That(typeof(BonakMarket.MultiSms.Services.SmsGatewaySender).Assembly.GetReferencedAssemblies().Any(x => x.Name == "BonakMarket.Identity"), Is.False);
        Assert.That(type.GetCustomAttribute<RouteAttribute>()?.Template, Is.EqualTo("api/identity/v1/auth/sms"));
        Assert.That(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpPostAttribute>().Any()).Select(m => m.Name),
            Is.EquivalentTo(new[] { nameof(SmsLoginApiController.RequestCode), nameof(SmsLoginApiController.VerifyCode) }));
        Assert.That(type.GetMethod(nameof(SmsLoginApiController.RequestCode))?.GetCustomAttributes<HttpPostAttribute>().Select(a => a.Template),
            Is.EquivalentTo(new[] { "request" }));
        Assert.That(typeof(ProfileApiController).GetCustomAttribute<RouteAttribute>()?.Template, Is.EqualTo("api/identity/v1/me"));
        Assert.That(typeof(ProfileApiController).GetMethod(nameof(ProfileApiController.GetProfile))?.GetCustomAttributes<HttpGetAttribute>().Select(a => a.Template),
            Is.EquivalentTo(new[] { "profile" }));
        Assert.That(typeof(ProfileApiController).GetMethod(nameof(ProfileApiController.UpdateProfile))?.GetCustomAttributes<HttpPutAttribute>().Select(a => a.Template),
            Is.EquivalentTo(new[] { "profile" }));
        Assert.That(typeof(AuthApiController).GetCustomAttribute<RouteAttribute>()?.Template, Is.EqualTo("api/identity/v1/auth"));
        Assert.That(typeof(AuthApiController).GetMethod(nameof(AuthApiController.Logout))?.GetCustomAttributes<HttpPostAttribute>().Select(a => a.Template),
            Is.EquivalentTo(new[] { "logout" }));
        Assert.That(typeof(AccountSecurityApiController).GetCustomAttribute<RouteAttribute>()?.Template, Is.EqualTo("api/identity/v1/me/security/phone"));
        Assert.That(typeof(AccountSecurityApiController).GetMethod(nameof(AccountSecurityApiController.ConfirmEnrollment))?.GetCustomAttributes<HttpPostAttribute>().Select(a => a.Template),
            Is.EquivalentTo(new[] { "verify" }));
        Assert.That(typeof(BonakMarket.Identity.Services.ISmsOtpLoginService).GetMethod("CompleteProfileAsync"), Is.Null);
        Assert.That(typeof(IStorefrontSmsProfileService).IsAssignableFrom(typeof(BonakMarket.Identity.Services.CustomerProfileService)), Is.True);
        Assert.That(typeof(StorefrontController).Assembly.GetType("BonakMarket.StorefrontApi.Controllers.ProfileController"), Is.Null);
        Assert.That(typeof(StorefrontController).Assembly.GetType("BonakMarket.StorefrontApi.Controllers.AuthController"), Is.Null);
    }

    [Test]
    public void PrivateControllersDoNotMarkActionsPublic()
    {
        var types = new[] { typeof(OrdersController), typeof(CartController), typeof(WishlistController),
            typeof(RewardPointsController), typeof(StockSubscriptionsController), typeof(ShipmentsController),
            typeof(RecurringPaymentsController), typeof(ReturnsController), typeof(DownloadsController),
            typeof(CheckoutController), typeof(CheckoutSessionsController), typeof(ReviewsController), typeof(SharingController) };
        foreach (var type in types)
        {
            Assert.That(type.GetCustomAttributes<TypeFilterAttribute>(true).Any(x => x.ImplementationType == typeof(StorefrontAuthorizationFilter)), Is.True);
            Assert.That(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Any(m => m.IsDefined(typeof(PublicCatalogAttribute), true)), Is.False);
        }
    }
}
