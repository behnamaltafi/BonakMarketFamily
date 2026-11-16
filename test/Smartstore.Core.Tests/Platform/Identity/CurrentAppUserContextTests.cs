using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using Smartstore.Core.Identity;

namespace Smartstore.Core.Tests.Platform.Identity;

[TestFixture]
public class CurrentAppUserContextTests
{
    [Test]
    public async Task AnonymousRequestDoesNotResolveAnAppUser()
    {
        var contextAccessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var sut = new CurrentAppUserContext(contextAccessor, null);

        Assert.That(await sut.GetCurrentUserAsync(), Is.Null);
    }

    [Test]
    public async Task LegacyCustomerIdentifierDoesNotResolveAnAppUser()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "321") }, "LegacyCustomerCookie"))
        };

        var sut = new CurrentAppUserContext(
            new HttpContextAccessor { HttpContext = httpContext }, null);

        Assert.That(await sut.GetCurrentUserAsync(), Is.Null);
    }

    [Test]
    public async Task MalformedAppUserClaimIsRejected()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(CurrentAppUserContext.UserGuidClaim, "malformed") }, "AppUserCookie"))
        };

        var sut = new CurrentAppUserContext(
            new HttpContextAccessor { HttpContext = httpContext }, null);

        Assert.That(await sut.GetCurrentUserAsync(), Is.Null);
    }
}
