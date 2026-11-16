using Microsoft.AspNetCore.Identity;
using System;
using System.Threading;
using System.Threading.Tasks;
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
    public async Task MalformedClaimIsDetectedAsAppUserSessionAndRejected()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(CurrentAppUserContext.UserGuidClaim, "malformed", ClaimValueTypes.String, CurrentAppUserContext.TrustedClaimIssuer) }, IdentityConstants.ApplicationScheme))
        };
        var sut = new CurrentAppUserContext(
            new HttpContextAccessor { HttpContext = context }, null);

        Assert.That(sut.IsAppUserIdentityPresent, Is.True);
        Assert.That(await sut.GetCurrentUserAsync(), Is.Null);
    }

    [Test]
    public async Task LegacyCustomerSessionDoesNotDeclareAppUserIdentity()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "321") }, "LegacyCustomerCookie"))
        };
        var sut = new CurrentAppUserContext(
            new HttpContextAccessor { HttpContext = context }, null);

        Assert.That(sut.IsAppUserIdentityPresent, Is.False);
        Assert.That(await sut.GetCurrentUserAsync(), Is.Null);
    }

    [Test]
    public async Task MalformedAppUserClaimIsRejected()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(CurrentAppUserContext.UserGuidClaim, "malformed", ClaimValueTypes.String, CurrentAppUserContext.TrustedClaimIssuer) }, IdentityConstants.ApplicationScheme))
        };

        var sut = new CurrentAppUserContext(
            new HttpContextAccessor { HttpContext = httpContext }, null);

        Assert.That(await sut.GetCurrentUserAsync(), Is.Null);
    }
    [Test]
    public async Task ExternalProviderClaimCannotImpersonateAppUser()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(CurrentAppUserContext.UserGuidClaim,
                        Guid.NewGuid().ToString("D"),
                        ClaimValueTypes.String,
                        CurrentAppUserContext.TrustedClaimIssuer)
                },
                IdentityConstants.ExternalScheme))
        };

        var sut = new CurrentAppUserContext(
            new HttpContextAccessor { HttpContext = context }, null);

        Assert.That(sut.IsAppUserIdentityPresent, Is.False);
        Assert.That(await sut.GetCurrentUserAsync(), Is.Null);
    }

    [Test]
    public async Task ClaimedAppUserFromUntrustedIssuerCannotImpersonate()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(CurrentAppUserContext.UserGuidClaim,
                    Guid.NewGuid().ToString("D"), ClaimValueTypes.String, "ExternalIssuer") },
                IdentityConstants.ApplicationScheme))
        };

        var sut = new CurrentAppUserContext(
            new HttpContextAccessor { HttpContext = context }, null);

        Assert.That(sut.IsAppUserIdentityPresent, Is.False);
        Assert.That(await sut.GetCurrentUserAsync(), Is.Null);
    }


    [Test]
    public async Task AppUserWithoutSignedLegacyLoginIdIsRejected()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(CurrentAppUserContext.UserGuidClaim,
                    Guid.NewGuid().ToString("D"), ClaimValueTypes.String,
                    CurrentAppUserContext.TrustedClaimIssuer) },
                IdentityConstants.ApplicationScheme))
        };
        var sut = new CurrentAppUserContext(new HttpContextAccessor { HttpContext = context }, null);

        Assert.That(sut.IsAppUserIdentityPresent, Is.True);
        Assert.That(await sut.GetCurrentUserAsync(), Is.Null);
    }

    [Test]
    public async Task DuplicateTrustedAppUserClaimsFailClosed()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(CurrentAppUserContext.UserGuidClaim, Guid.NewGuid().ToString("D"),
                        ClaimValueTypes.String, CurrentAppUserContext.TrustedClaimIssuer),
                    new Claim(CurrentAppUserContext.UserGuidClaim, Guid.NewGuid().ToString("D"),
                        ClaimValueTypes.String, CurrentAppUserContext.TrustedClaimIssuer),
                    new Claim(ClaimTypes.NameIdentifier, "10")
                }, IdentityConstants.ApplicationScheme))
        };
        var sut = new CurrentAppUserContext(new HttpContextAccessor { HttpContext = context }, null);

        Assert.That(sut.IsAppUserIdentityPresent, Is.True);
        Assert.That(await sut.GetCurrentUserAsync(), Is.Null);
    }

    [Test]
    public async Task InvalidLegacyLoginIdFailsBeforeDatabaseLookup()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(CurrentAppUserContext.UserGuidClaim, Guid.NewGuid().ToString("D"),
                        ClaimValueTypes.String, CurrentAppUserContext.TrustedClaimIssuer),
                    new Claim(ClaimTypes.NameIdentifier, "not-an-id")
                }, IdentityConstants.ApplicationScheme))
        };
        var sut = new CurrentAppUserContext(new HttpContextAccessor { HttpContext = context }, null);

        Assert.That(await sut.GetCurrentUserAsync(), Is.Null);
    }

}
