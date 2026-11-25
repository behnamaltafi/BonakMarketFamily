using NUnit.Framework;
using BonakMarket.MultiSms.Services;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class SmsIrProviderTests
{
    [TestCase("{\"status\":1,\"data\":{\"messageIds\":[12345678901]}}", false)]
    [TestCase("{\"status\":1,\"data\":{\"messageId\":12345678901}}", true)]
    public void Positive_confirmation_returns_id(string raw, bool verify)
        => Assert.That(SmsIrSmsProvider.ParseConfirmation(raw, 200, verify), Is.EqualTo("12345678901"));

    [TestCase("{\"status\":1,\"data\":{\"messageIds\":[0]}}", false, true)]
    [TestCase("{\"status\":1,\"data\":{\"messageIds\":[null]}}", false, true)]
    [TestCase("{\"status\":113}", true, true)]
    [TestCase("{\"status\":0}", false, false)]
    [TestCase("{\"status\":1,\"data\":{\"messageIds\":[]}}", false, false)]
    [TestCase("{\"status\":1,\"data\":{\"messageId\":0}}", true, false)]
    [TestCase("not-json", false, false)]
    public void Rejections_distinguish_safe_failover_from_unknown(string raw, bool verify, bool safe)
    {
        var exception = Assert.Throws<SmsProviderException>(() => SmsIrSmsProvider.ParseConfirmation(raw, 200, verify));
        Assert.That(exception.SafeToFailover, Is.EqualTo(safe));
    }

    [Test]
    public void Server_error_remains_ambiguous()
    {
        var exception = Assert.Throws<SmsProviderException>(() => SmsIrSmsProvider.ParseConfirmation("{\"status\":102}", 500, false));
        Assert.That(exception.SafeToFailover, Is.False);
    }
}
