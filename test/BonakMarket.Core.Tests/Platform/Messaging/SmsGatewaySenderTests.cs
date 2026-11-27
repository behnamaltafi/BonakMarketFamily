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
public class SmsGatewaySenderTests
{
    private static readonly SmsSendRequest Request = new("+989121234567", "Test message");

    [Test]
    public async Task Send_selects_provider_from_current_settings_on_every_call()
    {
        var first = Settings("Kavenegar");
        var second = Settings("Melipayamak");
        var settingsFactory = new Mock<ISettingFactory>(MockBehavior.Strict);
        settingsFactory.SetupSequence(x => x.LoadSettingsAsync<SmsGatewaySettings>(0))
            .ReturnsAsync(first)
            .ReturnsAsync(second);

        var kavenegar = new FakeProvider("Kavenegar");
        var melipayamak = new FakeProvider("Melipayamak");
        var sender = CreateSender(settingsFactory.Object, kavenegar, melipayamak);

        var a = await sender.SendAsync(Request);
        var b = await sender.SendAsync(Request);

        Assert.Multiple(() =>
        {
            Assert.That(a.Provider, Is.EqualTo("Kavenegar"));
            Assert.That(b.Provider, Is.EqualTo("Melipayamak"));
            Assert.That(kavenegar.Calls, Is.EqualTo(1));
            Assert.That(melipayamak.Calls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Send_falls_back_after_definite_provider_rejection()
    {
        var config = Settings("Kavenegar");
        config.FreeTextFailover = true;
        config.FreeTextFallback1 = "Melipayamak";

        var primary = new FakeProvider("Kavenegar")
        {
            Error = new SmsProviderException("Rejected.", safeToFailover: true)
        };
        var backup = new FakeProvider("Melipayamak");
        var sender = CreateSender(config, primary, backup);

        var receipt = await sender.SendAsync(Request);

        Assert.Multiple(() =>
        {
            Assert.That(receipt.Provider, Is.EqualTo("Melipayamak"));
            Assert.That(primary.Calls, Is.EqualTo(1));
            Assert.That(backup.Calls, Is.EqualTo(1));
        });
    }

    [Test]
    public void Send_does_not_resend_after_ambiguous_provider_failure()
    {
        var config = Settings("Kavenegar");
        config.FreeTextFailover = true;
        config.FreeTextFallback1 = "Melipayamak";

        var primary = new FakeProvider("Kavenegar") { Error = new HttpRequestException("Transport lost.") };
        var backup = new FakeProvider("Melipayamak");
        var sender = CreateSender(config, primary, backup);

        Assert.ThrowsAsync<HttpRequestException>(async () => await sender.SendAsync(Request));
        Assert.That(backup.Calls, Is.Zero);
    }

    [Test]
    public void Send_does_not_resend_to_the_same_provider()
    {
        var config = Settings("Kavenegar");
        config.FreeTextFailover = true;
        config.FreeTextFallback1 = "Kavenegar";

        var primary = new FakeProvider("Kavenegar")
        {
            Error = new SmsProviderException("Rejected.", safeToFailover: true)
        };
        var sender = CreateSender(config, primary);

        Assert.ThrowsAsync<SmsProviderException>(async () => await sender.SendAsync(Request));
        Assert.That(primary.Calls, Is.EqualTo(1));
    }

    [Test]
    public void Send_rejects_disabled_gateway()
    {
        var config = Settings("Kavenegar");
        config.Enabled = false;
        var primary = new FakeProvider("Kavenegar");
        var sender = CreateSender(config, primary);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await sender.SendAsync(Request));
        Assert.That(primary.Calls, Is.Zero);
    }

    [Test]
    public void Send_rejects_bad_phone_number_before_provider_call()
    {
        var primary = new FakeProvider("Kavenegar");
        var sender = CreateSender(Settings("Kavenegar"), primary);

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await sender.SendAsync(new SmsSendRequest("0912,0913", "test")));
        Assert.That(primary.Calls, Is.Zero);
    }

    [Test]
    public void Otp_never_uses_a_free_text_only_provider()
    {
        var config = Settings("Kavenegar");
        config.TemplatePrimary = "Kavenegar";
        var provider = new FakeProvider("Kavenegar");
        var sender = CreateSender(config, provider);
        Assert.ThrowsAsync<SmsProviderException>(async () => await sender.SendAsync(
            new SmsSendRequest("09123456789", "OTP", SmsMessageKind.Otp) { VerificationCode = "123456" }));
        Assert.That(provider.Calls, Is.Zero);
    }

    [Test]
    public async Task General_route_supports_two_ordered_fallbacks()
    {
        var config = Settings("Kavenegar");
        config.FreeTextFailover = true;
        config.FreeTextFallback1 = "Melipayamak";
        config.FreeTextFallback2 = "SmsIr";
        var first = new FakeProvider("Kavenegar") { Error = new SmsProviderException("Rejected", true) };
        var second = new FakeProvider("Melipayamak") { Error = new SmsProviderException("Rejected", true) };
        var third = new FakeProvider("SmsIr");
        var result = await CreateSender(config, first, second, third).SendAsync(Request);
        Assert.That(result.Provider, Is.EqualTo("SmsIr"));
        Assert.That(first.Calls, Is.EqualTo(1));
        Assert.That(second.Calls, Is.EqualTo(1));
        Assert.That(third.Calls, Is.EqualTo(1));
    }

    private static SmsGatewaySettings Settings(string primary) => new()
    {
        Enabled = true,
        DeliveryRoutingConfigured = true,
        FreeTextPrimary = primary
    };

    private static SmsGatewaySender CreateSender(SmsGatewaySettings settings, params ISmsProvider[] providers)
    {
        var factory = new Mock<ISettingFactory>();
        factory.Setup(x => x.LoadSettingsAsync<SmsGatewaySettings>(0)).ReturnsAsync(settings);
        return CreateSender(factory.Object, providers);
    }

    private static SmsGatewaySender CreateSender(ISettingFactory factory, params ISmsProvider[] providers)
        => new(factory, providers, NullLogger<SmsGatewaySender>.Instance, templates: TestSmsTemplates.Catalog());

    private sealed class FakeProvider : ISmsProvider
    {
        public FakeProvider(string name) => Name = name;

        public string Name { get; }
        public int Calls { get; private set; }
        public Exception Error { get; set; }
        public bool Enabled { get; set; } = true;

        public bool IsAvailable(SmsGatewaySettings settings) => Enabled;

        public Task<string> SendAsync(string phone, string text, SmsGatewaySettings settings, CancellationToken cancelToken)
        {
            Calls++;
            if (Error != null)
            {
                throw Error;
            }

            return Task.FromResult("test-message-id");
        }
    }
}
