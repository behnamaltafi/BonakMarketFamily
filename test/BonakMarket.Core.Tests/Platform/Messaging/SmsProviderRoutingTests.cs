using NUnit.Framework;
using BonakMarket.MultiSms.Configuration;
using BonakMarket.MultiSms.Services;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class SmsProviderRoutingTests
{
    [Test]
    public void Legacy_configuration_requires_reconfiguration()
    {
        var settings = new SmsGatewaySettings { Enabled = true, PrimaryProvider = "Kavenegar" };
        Assert.That(SmsRouting.Providers(settings, true), Is.Empty);
        Assert.That(SmsRouting.Providers(settings, false), Is.Empty);
    }

    [Test]
    public void Otp_and_general_have_independent_routes()
    {
        var settings = new SmsGatewaySettings
        {
            RoutingConfigured = true, OtpPrimary = "SmsIr", OtpFailover = true,
            OtpFallback1 = "Kavenegar", GeneralPrimary = "Melipayamak"
        };
        Assert.That(SmsRouting.Providers(settings, true), Is.EqualTo(new[] { "SmsIr", "Kavenegar" }));
        Assert.That(SmsRouting.Providers(settings, false), Is.EqualTo(new[] { "Melipayamak" }));
    }

    [Test]
    public void Mock_must_be_isolated()
    {
        var settings = new SmsGatewaySettings
        {
            RoutingConfigured = true, OtpPrimary = "Mock", OtpFailover = true, OtpFallback1 = "SmsIr"
        };
        Assert.That(SmsRouting.Providers(settings, true), Is.Empty);
    }

    [Test]
    public void Otp_only_configuration_does_not_require_a_sender()
    {
        var s = new SmsGatewaySettings
        {
            KavenegarEnabled = true, KavenegarOtpEnabled = true,
            KavenegarOtpTemplate = "login", EncryptedKavenegarApiKey = "key",
            SmsIrEnabled = true, SmsIrOtpEnabled = true, SmsIrTemplateId = 123,
            EncryptedSmsIrApiKey = "key",
            MelipayamakEnabled = true, MelipayamakOtpEnabled = true,
            MelipayamakBodyId = 42, EncryptedMelipayamakApiToken = "key"
        };
        foreach (ISmsProvider provider in new ISmsProvider[] { new KavenegarSmsProvider(null), new SmsIrSmsProvider(null), new MelipayamakSmsProvider(null) })
        {
            Assert.That(SmsRouting.IsReady(provider, s, true), Is.True);
            Assert.That(SmsRouting.IsReady(provider, s, false), Is.False);
        }
    }

    [TestCase("{\"return\":{\"status\":200},\"entries\":{\"messageid\":12345678901}}")]
    [TestCase("{\"return\":{\"status\":200},\"entries\":[{\"messageid\":12345678901}]}")]
    public void Kavenegar_accepts_documented_receipt_shapes(string json)
        => Assert.That(KavenegarSmsProvider.ParseConfirmation(json, 200), Is.EqualTo("12345678901"));

    [TestCase("{\"recId\":12345678901}", true)]
    [TestCase("{\"RetStatus\":1,\"Value\":\"12345678901\"}", false)]
    public void Melipayamak_accepts_only_positive_receipts(string json, bool console)
        => Assert.That(MelipayamakSmsProvider.ParseConfirmation(json, 200, console), Is.EqualTo("12345678901"));

    [TestCase("{\"status\":\"ok\"}", 200)]
    [TestCase("{\"recId\":0}", 200)]
    [TestCase("not JSON", 200)]
    [TestCase("{\"recId\":12345678901}", 500)]
    public void Unknown_console_receipt_does_not_allow_failover(string json, int status)
    {
        var ex = Assert.Throws<SmsProviderException>(() => MelipayamakSmsProvider.ParseConfirmation(json, status, true));
        Assert.That(ex.SafeToFailover, Is.False);
    }
}
