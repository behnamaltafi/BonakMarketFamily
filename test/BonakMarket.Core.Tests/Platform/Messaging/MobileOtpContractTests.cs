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
    public void Default_otp_message_preserves_persian_text_and_line_breaks()
    {
        var settings = new CustomerIdentitySettings();
        Assert.That(settings.FormatOtpMessage("012345"), Is.EqualTo(
            "بنک مارکت\n«محرمانه»\nاز در اختیار قرار دادن محتوای این پیامک به دیگران، اکیدا خودداری کنید.\nکد احراز هویت جهت ورود:\n012345"));
    }

    [Test]
    public void Custom_otp_message_replaces_placeholder()
    {
        var settings = new CustomerIdentitySettings { OtpMessageTemplate = "Custom\n{Code}" };
        Assert.That(settings.FormatOtpMessage("012345"), Is.EqualTo("Custom\n012345"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("No placeholder")]
    [TestCase("{code}")]
    public void Invalid_otp_message_uses_default(string template)
    {
        Assert.That(CustomerIdentitySettings.IsValidOtpMessageTemplate(template), Is.False);
        var settings = new CustomerIdentitySettings { OtpMessageTemplate = template };
        Assert.That(settings.FormatOtpMessage("012345"), Is.EqualTo(new CustomerIdentitySettings().FormatOtpMessage("012345")));
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
