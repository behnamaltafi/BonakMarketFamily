using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.DataProtection;
using NUnit.Framework;
using BonakMarket.MultiSms.Configuration;
using BonakMarket.MultiSms.Services;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class SmsReadinessTests
{
    private static SmsGatewaySettings Config() => new()
    {
        Enabled = true, DeliveryRoutingConfigured = true, TemplatePrimary = "Melipayamak",
        MelipayamakEnabled = true, MelipayamakTemplateEnabled = true, MelipayamakAuthMode = "Console"
    };

    private static SmsTemplateDefinition Template(bool mapped = true, string slot = "0")
        => new("LOGINOTP", true, true, new[] { "Code" },
            mapped ? new Dictionary<string, SmsTemplateMapping>(StringComparer.OrdinalIgnoreCase)
            {
                ["Melipayamak"] = new("123", new Dictionary<string, string> { ["Code"] = slot })
            } : new Dictionary<string, SmsTemplateMapping>());

    [Test]
    public void Console_credentials_missing_is_reported_specifically()
    {
        var config = Config();
        var provider = new MelipayamakSmsProvider(null);
        var result = SmsReadinessService.Evaluate(config, new[] { provider }, Template());
        Assert.That(result.IsReady, Is.False);
        Assert.That(result.Issues[0].Code, Is.EqualTo("ConsoleTokenMissing"));
    }

    [Test]
    public void Valid_connection_does_not_imply_valid_template_mapping()
    {
        var config = Config();
        config.EncryptedMelipayamakApiToken = "test";
        var provider = new MelipayamakSmsProvider(null);
        Assert.That(SmsRouting.IsReady(provider, config, true), Is.True);
        var missing = SmsReadinessService.Evaluate(config, new[] { provider }, Template(false));
        var invalid = SmsReadinessService.Evaluate(config, new[] { provider }, Template(slot: "1"));
        Assert.That(missing.Issues[0].Code, Is.EqualTo("MissingMapping"));
        Assert.That(invalid.Issues[0].Code, Is.EqualTo("InvalidMapping"));
        Assert.That(SmsReadinessService.Evaluate(config, new[] { provider }, Template()).IsReady, Is.True);
    }

    [Test]
    public void Credential_decryption_is_part_of_readiness()
    {
        var config = Config();
        var keys = new SmsCredentialProtection(new EphemeralDataProtectionProvider());
        config.EncryptedMelipayamakApiToken = keys.Protect("token");
        var current = new MelipayamakSmsProvider(keys);
        var rotated = new MelipayamakSmsProvider(new SmsCredentialProtection(new EphemeralDataProtectionProvider()));
        Assert.That(SmsReadinessService.Evaluate(config, new[] { current }, Template()).IsReady, Is.True);
        Assert.That(SmsReadinessService.Evaluate(config, new[] { rotated }, Template()).Issues[0].Code,
            Is.EqualTo("CredentialsDecryptionFailed"));
    }

    [Test]
    public void A_configured_provider_outside_the_route_is_not_ready()
    {
        var config = Config();
        config.EncryptedMelipayamakApiToken = "test";
        config.TemplatePrimary = "SmsIr";
        var result = SmsReadinessService.Evaluate(config, new ISmsProvider[] { new MelipayamakSmsProvider(null) }, Template());
        Assert.That(result.IsReady, Is.False);
        Assert.That(result.Issues[0].Code, Is.EqualTo("UnknownProvider"));
    }
}
