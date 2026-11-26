using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.DataProtection;
using NUnit.Framework;
using BonakMarket.Identity.Configuration;
using BonakMarket.Identity.Models;
using BonakMarket.Identity.Security;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class MobileOtpContractTests
{
    [Test]
    public void Protected_ticket_binds_phone_and_send_generation()
    {
        var service = new MobileOtpChallengeTicket(new EphemeralDataProtectionProvider());
        var now = DateTime.UtcNow.Ticks;
        var ticket = service.Create("+989121234567", now);
        Assert.That(ticket, Does.Not.Contain("+989121234567"));
        Assert.That(service.TryRead(ticket, out var phone, out var sent), Is.True);
        Assert.That(phone, Is.EqualTo("+989121234567"));
        Assert.That(sent, Is.EqualTo(now));
        Assert.That(service.TryRead(service.Create("+989121234567", 0), out _, out _), Is.False);
        Assert.That(service.TryRead("not-a-ticket", out _, out _), Is.False);
        Assert.That(new MobileOtpChallengeTicket(new EphemeralDataProtectionProvider()).TryRead(ticket, out _, out _), Is.False);
    }

    [Test]
    public void All_otp_flows_default_to_the_seeded_template()
    {
        var settings = new CustomerIdentitySettings();
        Assert.Multiple(() =>
        {
            Assert.That(settings.GetOtpTemplateKey("login"), Is.EqualTo("LOGINOTP"));
            Assert.That(settings.GetOtpTemplateKey("enroll:42"), Is.EqualTo("LOGINOTP"));
            Assert.That(settings.GetOtpTemplateKey("reauth:42"), Is.EqualTo("LOGINOTP"));
        });
    }

    [Test]
    public void Otp_flows_resolve_independent_logical_keys()
    {
        var settings = new CustomerIdentitySettings
        {
            LoginOtpTemplateKey = " SignIn ",
            PhoneEnrollmentOtpTemplateKey = "PhoneOtp",
            ReauthenticationOtpTemplateKey = "ReauthOtp"
        };
        Assert.Multiple(() =>
        {
            Assert.That(settings.GetOtpTemplateKey("login"), Is.EqualTo("SIGNIN"));
            Assert.That(settings.GetOtpTemplateKey("enroll:42"), Is.EqualTo("PHONEOTP"));
            Assert.That(settings.GetOtpTemplateKey("reauth:42"), Is.EqualTo("REAUTHOTP"));
            Assert.That(settings.GetOtpTemplateKey("enroll:100"), Is.EqualTo("PHONEOTP"));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void Missing_legacy_template_setting_uses_default(string key)
    {
        var settings = new CustomerIdentitySettings { LoginOtpTemplateKey = key };
        Assert.That(settings.GetOtpTemplateKey("login"), Is.EqualTo("LOGINOTP"));
    }

    [Test]
    public void Unknown_otp_purpose_is_not_silently_mapped_to_login()
    {
        Assert.Throws<ArgumentException>(() => new CustomerIdentitySettings().GetOtpTemplateKey("reauth-proof:42"));
    }

    [TestCase("012345", true)]
    [TestCase("12345", false)]
    [TestCase("1234567", false)]
    [TestCase("abc123", false)]
    public void Verification_requires_six_ascii_digits(string code, bool expected)
    {
        var dto = new MobileSmsVerifyRequest { ChallengeId = "test", Code = code };
        Assert.That(Validator.TryValidateObject(dto, new ValidationContext(dto),
            new List<ValidationResult>(), true), Is.EqualTo(expected));
    }
}
