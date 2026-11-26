using System;
using Microsoft.AspNetCore.DataProtection;
using NUnit.Framework;
using BonakMarket.Core.Messaging;
using BonakMarket.MultiSms.Configuration;
using BonakMarket.MultiSms.Domain;
using BonakMarket.MultiSms.Services;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class OtpHistoryProtectionTests
{
    private static SmsSendRequest Otp => new("09123456789", "Secret 012345", SmsMessageKind.Otp) { VerificationCode = "012345" };

    [Test]
    public void Disabled_by_default_and_does_not_capture_test_or_missing_explicit_code()
    {
        var protection = new OtpHistoryProtection(new EphemeralDataProtectionProvider());
        var settings = new SmsGatewaySettings();
        Assert.That(protection.Capture(Otp, settings), Is.Null);
        settings.ShowOtpCodeInHistory = true;
        Assert.That(protection.Capture(Otp with { Kind = SmsMessageKind.Test }, settings), Is.Null);
        Assert.That(protection.Capture(Otp with { VerificationCode = null }, settings), Is.Null);
        settings.HistoryEnabled = false;
        Assert.That(protection.Capture(Otp, settings), Is.Null);
    }

    [Test]
    public void Protected_code_is_visible_immediately_in_all_environments()
    {
        var protection = new OtpHistoryProtection(new EphemeralDataProtectionProvider());
        var settings = new SmsGatewaySettings { ShowOtpCodeInHistory = true };
        var now = DateTime.UtcNow;
        var message = new SmsMessage { Kind = "Otp", CreatedOnUtc = now, Body = protection.Capture(Otp, settings) };
        Assert.That(message.Body, Is.Not.Null.And.Not.EqualTo("012345"));
        Assert.That(protection.Reveal(message, settings, true, now), Is.EqualTo("012345"));
        Assert.That(protection.Reveal(message, settings, false, now), Is.EqualTo("012345"));
        Assert.That(protection.Reveal(message, settings, false, now.AddMinutes(5)), Is.EqualTo("012345"));
        Assert.That(protection.Reveal(message, settings, true, now.AddMinutes(15)), Is.Null);
        settings.ShowOtpCodeInHistory = false;
        Assert.That(protection.Reveal(message, settings, true, now), Is.Null);
    }

    [Test]
    public void Invalid_ciphertext_never_reveals_plaintext()
    {
        var protection = new OtpHistoryProtection(new EphemeralDataProtectionProvider());
        var settings = new SmsGatewaySettings { ShowOtpCodeInHistory = true };
        var now = DateTime.UtcNow;
        Assert.That(protection.Reveal(new SmsMessage { Kind = "Otp", CreatedOnUtc = now, Body = "012345" }, settings, true, now), Is.Null);
    }
}
