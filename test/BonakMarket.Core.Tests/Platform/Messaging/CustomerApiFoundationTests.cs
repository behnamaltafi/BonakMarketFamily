using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using BonakMarket.Identity.Filters;
using BonakMarket.Identity.Models;
using BonakMarket.StorefrontApi.Models;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class CustomerApiFoundationTests
{
    [TestCase("/api/identity/v1/me/profile", true)]
    [TestCase("/api/storefront/v1/cart", true)]
    [TestCase("/api/identity/v10/me/profile", false)]
    [TestCase("/api/sms-login/profile", false)]
    [TestCase("/odata/v1/Products", false)]
    [TestCase("/checkout", false)]
    public void Only_canonical_customer_routes_are_normalized(string path, bool expected)
        => Assert.That(CustomerApiProblemFilter.IsCanonicalPath(new PathString(path)), Is.EqualTo(expected));

    [Test]
    public void Typed_identity_error_preserves_machine_code()
    {
        var http = new DefaultHttpContext();
        var result = (ObjectResult)CustomerApiProblemFilter.Normalize(
            new BadRequestObjectResult(new IdentityCodeResponse("invalid_code")), http);
        var problem = (ProblemDetails)result.Value;
        Assert.That(result.StatusCode, Is.EqualTo(400));
        Assert.That(result.ContentTypes, Does.Contain("application/problem+json"));
        Assert.That(problem.Extensions["code"], Is.EqualTo("invalid_code"));
        Assert.That(problem.Extensions["traceId"], Is.Not.Null);
    }

    [Test]
    public void Otp_error_preserves_specific_code_and_retry_after()
    {
        var result = (ObjectResult)CustomerApiProblemFilter.Normalize(
            new ObjectResult(new OtpErrorResponse("otp_resend_too_soon", 37)) { StatusCode = 429 },
            new DefaultHttpContext());
        var problem = (ProblemDetails)result.Value;
        Assert.That(problem.Extensions["code"], Is.EqualTo("otp_resend_too_soon"));
        Assert.That(problem.Extensions["retryAfter"], Is.EqualTo(37));
    }

    [Test]
    public void Native_validation_errors_and_warnings_are_preserved()
    {
        var http = new DefaultHttpContext();
        var original = new ValidationProblemDetails(new Dictionary<string, string[]> { ["quantity"] = new[] { "required" } })
            { Status = 422, Title = "cart_validation_failed" };
        original.Extensions["warnings"] = new[] { "warning" };
        var result = (ObjectResult)CustomerApiProblemFilter.Normalize(new ObjectResult(original) { StatusCode = 422 }, http);
        Assert.That(result.Value, Is.SameAs(original));
        Assert.That(original.Extensions["code"], Is.EqualTo("cart_validation_failed"));
        Assert.That(original.Extensions.ContainsKey("warnings"), Is.True);
        Assert.That(original.Errors.ContainsKey("quantity"), Is.True);
    }

    [Test]
    public void Successful_and_empty_responses_are_not_changed()
    {
        var http = new DefaultHttpContext();
        var result = new NoContentResult();
        Assert.That(CustomerApiProblemFilter.Normalize(result, http), Is.SameAs(result));
    }

    [Test]
    public void Empty_refresh_error_has_a_stable_code()
    {
        var http = new DefaultHttpContext();
        http.Request.Path = "/api/identity/v1/auth/refresh";
        var result = (ObjectResult)CustomerApiProblemFilter.Normalize(new UnauthorizedResult(), http);
        Assert.That(((ProblemDetails)result.Value).Extensions["code"], Is.EqualTo("invalid_refresh_token"));
    }

    [TestCase(1, 20, 0, 0, false, false)]
    [TestCase(1, 20, 21, 2, true, false)]
    [TestCase(2, 20, 21, 2, false, true)]
    public void Page_metadata_matches_across_modules(int page, int size, int count, int pages, bool next, bool previous)
    {
        var identity = new AddressPage(Array.Empty<CustomerAddressDto>(), page, size, count);
        var storefront = new PageDto<int>(Array.Empty<int>(), page, size, count, pages);
        Assert.That(identity.TotalPages, Is.EqualTo(pages));
        Assert.That(identity.HasNextPage, Is.EqualTo(next));
        Assert.That(identity.HasPreviousPage, Is.EqualTo(previous));
        Assert.That(storefront.HasNextPage, Is.EqualTo(next));
        Assert.That(storefront.HasPreviousPage, Is.EqualTo(previous));
    }
}
