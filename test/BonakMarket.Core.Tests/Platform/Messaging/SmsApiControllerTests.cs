using System;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Messaging;
using BonakMarket.Core.Security;
using BonakMarket.Events;
using BonakMarket.MultiSms.Controllers;
using BonakMarket.MultiSms.Models;
using BonakMarket.Identity.Services;
using BonakMarket.Identity.Controllers;
using BonakMarket.Identity.Models;
using BonakMarket.Identity.Configuration;
using BonakMarket.Core;
using Microsoft.Extensions.DependencyInjection;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class SmsApiControllerTests
{
    [Test]
    public async Task Login_requests_return_the_same_response_for_known_and_unknown_numbers()
    {
        var otp = new Mock<ISmsOtpLoginService>();
        otp.Setup(x => x.IsEnabledAsync()).ReturnsAsync(true);
        var controller = CreateLoginController(otp);

        var known = (OkObjectResult)await controller.RequestCode(new SmsCodeRequest { Phone = "09123456789" }, default);
        var unknown = (OkObjectResult)await controller.RequestCode(new SmsCodeRequest { Phone = "09351234567" }, default);

        Assert.That(known.Value, Is.TypeOf<MobileSmsChallengeResponse>());
        Assert.That(unknown.Value, Is.TypeOf<MobileSmsChallengeResponse>());
        Assert.That(((MobileSmsChallengeResponse)unknown.Value).ExpiresIn, Is.EqualTo(((MobileSmsChallengeResponse)known.Value).ExpiresIn));
        otp.Verify(x => x.RequestMobileChallengeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [TestCase("otp_resend_too_soon", 37)]
    [TestCase("otp_daily_limit", 7200)]
    public async Task Login_request_returns_rate_limit_and_retry_after(string code, int retryAfter)
    {
        var otp = new Mock<ISmsOtpLoginService>();
        otp.Setup(x => x.IsEnabledAsync()).ReturnsAsync(true);
        otp.Setup(x => x.RequestMobileChallengeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OtpFlowException(code, retryAfter));
        var controller = CreateLoginController(otp);
        var result = (ObjectResult)await controller.RequestCode(new SmsCodeRequest { Phone = "09123456789" }, default);
        Assert.That(result.StatusCode, Is.EqualTo(429));
        Assert.That(((OtpErrorResponse)result.Value).RetryAfter, Is.EqualTo(retryAfter));
        Assert.That(controller.Response.Headers["Retry-After"].ToString(), Is.EqualTo(retryAfter.ToString()));
    }

    [TestCase("otp_expired", 400)]
    [TestCase("otp_attempt_limit", 429)]
    public async Task Login_verification_returns_specific_otp_error(string code, int status)
    {
        var otp = new Mock<ISmsOtpLoginService>();
        otp.Setup(x => x.IsEnabledAsync()).ReturnsAsync(true);
        otp.Setup(x => x.ConfirmMobileLoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OtpFlowException(code));
        var result = (ObjectResult)await CreateLoginController(otp).VerifyCode(
            new MobileSmsVerifyRequest { ChallengeId = "challenge", Code = "123456" }, default);
        Assert.That(result.StatusCode, Is.EqualTo(status));
        Assert.That(((OtpErrorResponse)result.Value).Code, Is.EqualTo(code));
    }

    [Test]
    public async Task Disabled_login_does_not_issue_codes()
    {
        var otp = new Mock<ISmsOtpLoginService>();
        var result = await CreateLoginController(otp).RequestCode(new SmsCodeRequest { Phone = "09123456789" }, default);

        Assert.That(result, Is.TypeOf<NotFoundObjectResult>());
        otp.Verify(x => x.RequestMobileChallengeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Anonymous_enrollment_is_rejected_without_issuing_or_confirming_codes()
    {
        var otp = new Mock<ISmsOtpLoginService>();
        otp.Setup(x => x.IsEnabledAsync()).ReturnsAsync(true);
        var controller = CreateSecurityController(otp);

        Assert.That(await controller.RequestEnrollment(new PhoneChangeRequest(), default), Is.TypeOf<UnauthorizedResult>());
        Assert.That(await controller.ConfirmEnrollment(new PhoneChangeVerificationRequest(), default), Is.TypeOf<UnauthorizedResult>());
        otp.Verify(x => x.RequestEnrollmentCodeAsync(It.IsAny<Customer>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        otp.Verify(x => x.ConfirmEnrollmentAsync(It.IsAny<Customer>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Enrollment_uses_the_authenticated_customer()
    {
        var otp = new Mock<ISmsOtpLoginService>();
        otp.Setup(x => x.IsEnabledAsync()).ReturnsAsync(true);
        var customer = RegisteredCustomer();
        var users = CreateUserManager();
        users.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(customer);
        var signIn = CreateSignInManager(users);
        signIn.Setup(x => x.IsSignedIn(It.IsAny<ClaimsPrincipal>())).Returns(true);
        var controller = CreateSecurityController(otp, users, signIn, currentCustomer: customer);

        var result = await controller.RequestEnrollment(new PhoneChangeRequest { Phone = "09123456789" }, default);

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        otp.Verify(x => x.RequestEnrollmentCodeAsync(customer, "09123456789", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Bound_phone_change_without_reauth_cannot_use_initial_enrollment()
    {
        var otp = new Mock<ISmsOtpLoginService>();
        otp.Setup(x => x.IsEnabledAsync()).ReturnsAsync(true);
        var customer = RegisteredCustomer();
        otp.Setup(x => x.GetVerifiedPhoneAsync(customer.Id, It.IsAny<CancellationToken>())).ReturnsAsync("+989123456789");
        var users = CreateUserManager();
        users.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(customer);
        var signIn = CreateSignInManager(users);
        signIn.Setup(x => x.IsSignedIn(It.IsAny<ClaimsPrincipal>())).Returns(true);
        var controller = CreateSecurityController(otp, users, signIn, currentCustomer: customer);
        Assert.That(await controller.RequestEnrollment(new PhoneChangeRequest { Phone = "09351234567" }, default), Is.TypeOf<BadRequestObjectResult>());
        Assert.That(await controller.ConfirmEnrollment(new PhoneChangeVerificationRequest { Phone = "09351234567", Code = "123456" }, default), Is.TypeOf<BadRequestObjectResult>());
        otp.Verify(x => x.RequestEnrollmentCodeAsync(It.IsAny<Customer>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        otp.Verify(x => x.ConfirmEnrollmentAsync(It.IsAny<Customer>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Login_does_not_return_tokens_for_rejected_customer()
    {
        var otp = new Mock<ISmsOtpLoginService>();
        otp.Setup(x => x.IsEnabledAsync()).ReturnsAsync(true);
        var customer = RegisteredCustomer();
        otp.Setup(x => x.ConfirmMobileLoginAsync("challenge", "123456", It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        var tokens = new Mock<IMobileApiTokenService>();
        tokens.SetupGet(x => x.IsEnabled).Returns(true);
        var result = await CreateLoginController(otp, tokens: tokens).VerifyCode(
            new MobileSmsVerifyRequest { ChallengeId = "challenge", Code = "123456" }, default);
        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Verified_login_returns_only_tokens_and_updates_last_login()
    {
        var customer = RegisteredCustomer();
        var otp = new Mock<ISmsOtpLoginService>();
        otp.Setup(x => x.IsEnabledAsync()).ReturnsAsync(true);
        otp.Setup(x => x.ConfirmMobileLoginAsync("challenge", "123456", It.IsAny<CancellationToken>())).ReturnsAsync(customer);
        var tokens = new Mock<IMobileApiTokenService>();
        tokens.SetupGet(x => x.IsEnabled).Returns(true);
        tokens.Setup(x => x.IssueAsync(customer, It.IsAny<CancellationToken>())).ReturnsAsync(new MobileApiTokenPair("access", "refresh", 600));
        using var db = new Mock<SmartDbContext>(new DbContextOptions<SmartDbContext>()).Object;
        Mock.Get(db).Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var controller = CreateLoginController(otp, db: db, tokens: tokens);
        var result = (OkObjectResult)await controller.VerifyCode(new MobileSmsVerifyRequest { ChallengeId = "challenge", Code = "123456" }, default);
        Assert.That(((MobileSmsSignInResponse)result.Value).AccessToken, Is.EqualTo("access"));
        Assert.That(controller.Response.Headers.ContainsKey("Set-Cookie"), Is.False);
        Assert.That(customer.LastLoginDateUtc, Is.Not.Null);
        Mock.Get(db).Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Logout_with_refresh_revokes_without_rotating_or_using_cookie()
    {
        var tokens = new Mock<IMobileApiTokenService>(MockBehavior.Strict);
        var revoker = new Mock<ICustomerSessionRevoker>();
        revoker.Setup(x => x.RevokeRefreshAsync("refresh", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        var controller = new AuthApiController(tokens.Object, revoker.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
        Assert.That(await controller.Logout(new CustomerLogoutRequest { RefreshToken = "refresh" }, default), Is.TypeOf<NoContentResult>());
        revoker.Verify(x => x.RevokeRefreshAsync("refresh", It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(http.Response.Headers.ContainsKey("Set-Cookie"), Is.False);
        Assert.That(await controller.Logout(null, default), Is.TypeOf<UnauthorizedObjectResult>());
    }

    [Test]
    public async Task Logout_with_bearer_revokes_only_verified_api_session()
    {
        var customer = RegisteredCustomer();
        var work = new Mock<IWorkContext>();
        work.SetupGet(x => x.CurrentCustomer).Returns(customer);
        using var services = new ServiceCollection().AddSingleton(work.Object).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Scheme = "https";
        http.Request.Headers.Authorization = "Bearer access";
        http.Items[typeof(IMobileApiTokenService)] = customer;
        var tokens = new Mock<IMobileApiTokenService>();
        tokens.Setup(x => x.RevokeAsync("access", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var revoker = new Mock<ICustomerSessionRevoker>(MockBehavior.Strict);
        var controller = new AuthApiController(tokens.Object, revoker.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
        Assert.That(await controller.Logout(null, default), Is.TypeOf<NoContentResult>());
        Assert.That(await controller.Logout(new CustomerLogoutRequest { RefreshToken = "other" }, default),
            Is.TypeOf<BadRequestObjectResult>());
        tokens.Verify(x => x.RevokeAsync("access", It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(http.Response.Headers.ContainsKey("Set-Cookie"), Is.False);
    }

    [TestCase(false, false, false, 401)]
    [TestCase(true, false, false, 403)]
    [TestCase(true, true, false, 403)]
    [TestCase(true, false, true, 403)]
    public async Task Test_delivery_requires_a_native_session_backend_access_and_settings_permission(
        bool authenticated, bool backendAccess, bool settingsAccess, int status)
    {
        var customer = RegisteredCustomer();
        var users = CreateUserManager();
        users.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(customer);
        var signIn = CreateSignInManager(users);
        signIn.Setup(x => x.IsSignedIn(It.IsAny<ClaimsPrincipal>())).Returns(authenticated);
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(x => x.AuthorizeAsync(Permissions.System.AccessBackend, customer, false)).ReturnsAsync(backendAccess);
        permissions.Setup(x => x.AuthorizeAsync("configuration.setting", customer, false)).ReturnsAsync(settingsAccess);
        var sender = new Mock<ISmsSender>(MockBehavior.Strict);
        var controller = new MultiSmsApiController(sender.Object, users.Object, signIn.Object, permissions.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.TestSms(new SmsTestRequest { Phone = "09123456789", Text = "Test" }, default);

        int? actual = result is ObjectResult objectResult ? objectResult.StatusCode : ((StatusCodeResult)result).StatusCode;
        Assert.That(actual, Is.EqualTo(status));
        sender.Verify(x => x.SendAsync(It.IsAny<SmsSendRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static SmsLoginApiController CreateLoginController(Mock<ISmsOtpLoginService> otp,
        SmartDbContext db = null, Mock<IMobileApiTokenService> tokens = null)
    {
        otp.Setup(x => x.GetOtpSettingsAsync()).ReturnsAsync(new CustomerIdentitySettings());
        tokens ??= new Mock<IMobileApiTokenService>();
        tokens.SetupGet(x => x.IsEnabled).Returns(true);
        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        return new SmsLoginApiController(otp.Object, tokens.Object, db)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    private static AccountSecurityApiController CreateSecurityController(Mock<ISmsOtpLoginService> otp,
        Mock<UserManager<Customer>> users = null, Mock<SignInManager<Customer>> signIn = null, Customer currentCustomer = null)
    {
        users ??= CreateUserManager();
        signIn ??= CreateSignInManager(users);
        var work = new Mock<IWorkContext>();
        work.SetupGet(x => x.CurrentCustomer).Returns(currentCustomer);
        var services = new ServiceCollection().AddSingleton(work.Object).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Scheme = "https";
        http.Request.Method = HttpMethods.Post;
        if (currentCustomer != null) http.Items[typeof(IMobileApiTokenService)] = currentCustomer;
        return new AccountSecurityApiController(otp.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Profile_rejects_anonymous_requests_and_unvalidated_bearer(bool bearer)
    {
        var users = CreateUserManager();
        var signIn = CreateSignInManager(users);
        if (bearer) signIn.Setup(x => x.IsSignedIn(It.IsAny<ClaimsPrincipal>())).Returns(true);
        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        http.Request.Method = HttpMethods.Get;
        if (bearer) http.Request.Headers.Authorization = "Bearer invalid";
        var controller = new ProfileApiController(null, null)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
        Assert.That(await controller.GetProfile(default), Is.TypeOf<UnauthorizedResult>());
        var addresses = new CustomerAddressApiController(null)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
        Assert.That(await addresses.List(ct: default), Is.TypeOf<UnauthorizedResult>());
        Assert.That(await addresses.Get(99, default), Is.TypeOf<UnauthorizedResult>());
        users.Verify(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()), Times.Never);
    }

    [Test]
    public async Task Profile_rejects_a_mismatched_work_context_customer()
    {
        var customer = RegisteredCustomer();
        var users = CreateUserManager();
        var signIn = CreateSignInManager(users);
        signIn.Setup(x => x.IsSignedIn(It.IsAny<ClaimsPrincipal>())).Returns(true);
        users.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(customer);
        var work = new Mock<IWorkContext>();
        work.SetupGet(x => x.CurrentCustomer).Returns(new Customer { Id = 99 });
        using var services = new ServiceCollection().AddSingleton(work.Object).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Scheme = "https";
        http.Request.Method = HttpMethods.Get;
        http.Items[typeof(IMobileApiTokenService)] = customer;
        var controller = new ProfileApiController(null, null)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
        Assert.That(await controller.GetProfile(default), Is.TypeOf<UnauthorizedResult>());
    }

    private static Customer RegisteredCustomer()
    {
        var customer = new Customer { Id = 42, Active = true };
        customer.CustomerRoleMappings.Add(new CustomerRoleMapping
        {
            CustomerRole = new CustomerRole { Active = true, SystemName = SystemCustomerRoleNames.Registered }
        });
        return customer;
    }

    private static Mock<UserManager<Customer>> CreateUserManager()
        => new(Mock.Of<IUserStore<Customer>>(), Options.Create(new IdentityOptions()),
            Mock.Of<IPasswordHasher<Customer>>(), Array.Empty<IUserValidator<Customer>>(),
            Array.Empty<IPasswordValidator<Customer>>(), Mock.Of<ILookupNormalizer>(), new IdentityErrorDescriber(),
            Mock.Of<IServiceProvider>(), NullLogger<UserManager<Customer>>.Instance);

    private static Mock<SignInManager<Customer>> CreateSignInManager(Mock<UserManager<Customer>> users)
        => new(users.Object, Mock.Of<IHttpContextAccessor>(), Mock.Of<IUserClaimsPrincipalFactory<Customer>>(),
            Options.Create(new IdentityOptions()), NullLogger<SignInManager<Customer>>.Instance,
            Mock.Of<IAuthenticationSchemeProvider>(), Mock.Of<IUserConfirmation<Customer>>());
}
