using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using BonakMarket.Core.Configuration;
using BonakMarket.Core.Messaging;
using BonakMarket.MultiSms.Configuration;
using BonakMarket.MultiSms.Services;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class SmsTemplateRoutingTests
{
    private static SmsSendRequest Order() => new("09123456789", null)
    {
        DeliveryMode = SmsDeliveryMode.Template,
        TemplateKey = "OrderShipped",
        TemplateParameters = new Dictionary<string, string> { ["OrderNumber"] = "12", ["TrackingCode"] = "ABC" }
    };

    [Test]
    public async Task Non_otp_template_uses_template_route_and_provider_parameter_mapping()
    {
        var skipped = new Fake("SmsIr");
        var selected = new Fake("Melipayamak");
        var freeText = new Fake("Kavenegar");
        var sender = Sender(skipped, selected, freeText);
        var result = await sender.SendAsync(Order());
        Assert.Multiple(() =>
        {
            Assert.That(result.Provider, Is.EqualTo("Melipayamak"));
            Assert.That(skipped.Calls, Is.Zero);
            Assert.That(selected.Payload.ExternalTemplateId, Is.EqualTo("777"));
            Assert.That(selected.Payload.Parameters["0"], Is.EqualTo("ABC"));
            Assert.That(selected.Payload.Parameters["1"], Is.EqualTo("12"));
            Assert.That(freeText.Calls, Is.Zero);
        });
    }

    [Test]
    public void Otp_cannot_explicitly_select_freetext()
    {
        var fake = new Fake("Kavenegar");
        Assert.ThrowsAsync<ArgumentException>(async () => await Sender(fake).SendAsync(
            new SmsSendRequest("09123456789", "OTP", SmsMessageKind.Otp)
            { VerificationCode = "123456", DeliveryMode = SmsDeliveryMode.FreeText }));
        Assert.That(fake.Calls, Is.Zero);
    }

    [Test]
    public void Otp_template_cannot_be_used_as_general_message()
    {
        var fake = new Fake("Kavenegar");
        Assert.ThrowsAsync<ArgumentException>(async () => await Sender(fake).SendAsync(
            new SmsSendRequest("09123456789", null)
            { TemplateKey = "LOGINOTP", TemplateParameters = new Dictionary<string, string> { ["Code"] = "123456" } }));
        Assert.That(fake.Calls, Is.Zero);
    }

    [Test]
    public void Invalid_parameters_are_rejected_before_transport()
    {
        var fake = new Fake("Kavenegar");
        Assert.ThrowsAsync<ArgumentException>(async () => await Sender(fake).SendAsync(Order() with
            { TemplateParameters = new Dictionary<string, string> { ["OrderNumber"] = "12" } }));
        Assert.That(fake.Calls, Is.Zero);
    }

    [Test]
    public void Template_failure_does_not_switch_to_freetext()
    {
        var skipped = new Fake("SmsIr");
        var failing = new Fake("Melipayamak") { Error = new HttpRequestException("Timeout") };
        var freeText = new Fake("Kavenegar");
        Assert.ThrowsAsync<HttpRequestException>(async () => await Sender(skipped, failing, freeText).SendAsync(Order()));
        Assert.That(freeText.Calls, Is.Zero);
    }

    [Test]
    public void Provider_mapping_validates_slot_contracts()
    {
        Assert.That(new KavenegarSmsProvider(null).IsValidMapping("login", new[] { "token", "receptor" }), Is.False);
        Assert.That(new KavenegarSmsProvider(null).IsValidMapping("login", new[] { "token", "token2" }), Is.True);
        Assert.That(new MelipayamakSmsProvider(null).IsValidMapping("12", new[] { "1", "3" }), Is.False);
        Assert.That(new MelipayamakSmsProvider(null).IsValidMapping("12", new[] { "1", "0" }), Is.True);
        Assert.That(new SmsIrSmsProvider(null).IsValidMapping("123", new[] { "OrderNumber", "TRACKING" }), Is.True);
    }

    private static SmsGatewaySender Sender(params ISmsProvider[] providers)
    {
        var settings = new SmsGatewaySettings
        {
            Enabled = true, DeliveryRoutingConfigured = true, TemplatePrimary = "SmsIr", TemplateFailover = true,
            TemplateFallback1 = "Melipayamak", TemplateFallback2 = "Kavenegar", FreeTextPrimary = "Kavenegar"
        };
        var factory = new Mock<ISettingFactory>();
        factory.Setup(x => x.LoadSettingsAsync<SmsGatewaySettings>(0)).ReturnsAsync(settings);
        return new SmsGatewaySender(factory.Object, providers, NullLogger<SmsGatewaySender>.Instance,
            templates: TestSmsTemplates.Catalog());
    }
    private sealed class Fake(string name) : ISmsProvider, ITemplateSmsProvider
    {
        public string Name => name;
        public int Calls { get; private set; }
        public Exception Error { get; init; }
        public SmsTemplatePayload Payload { get; private set; }
        public bool IsAvailable(SmsGatewaySettings settings) => true;
        public bool IsTemplateAvailable(SmsGatewaySettings settings) => true;
        public bool IsValidMapping(string id, IEnumerable<string> slots) => true;
        public Task<string> SendAsync(string phone, string text, SmsGatewaySettings settings, CancellationToken ct)
            => throw new InvalidOperationException("Unexpected free-text call.");
        public Task<string> SendTemplateAsync(string phone, SmsTemplatePayload template, SmsGatewaySettings settings, CancellationToken ct)
        {
            Calls++;
            Payload = template;
            if (Error is not null) throw Error;
            return Task.FromResult("test-id");
        }
    }
}
