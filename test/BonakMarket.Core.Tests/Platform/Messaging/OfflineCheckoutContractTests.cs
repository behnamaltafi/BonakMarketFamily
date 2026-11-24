using NUnit.Framework;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class OfflineCheckoutContractTests
{
    [Test]
    public void Only_known_offline_provider_implementations_are_allowed()
    {
        Assert.That(OfflineCheckoutPolicy.IsSupported("Payments.CashOnDelivery",
            "BonakMarket.OfflinePayment.CashOnDeliveryProvider", "BonakMarket.OfflinePayment"), Is.True);
        Assert.That(OfflineCheckoutPolicy.IsSupported("Payments.Prepayment",
            "ThirdParty.Provider", "BonakMarket.OfflinePayment"), Is.False);
        Assert.That(OfflineCheckoutPolicy.IsSupported("Payments.PayPal",
            "ThirdParty.Provider", "PayPal"), Is.False);
    }

    [TestCase("00000000-0000-0000-0000-000000000000", false)]
    [TestCase("invalid", false)]
    [TestCase("d860dc5c-788b-48e1-abf5-5e3636cffc4a", true)]
    public void Idempotency_key_must_be_a_nonempty_guid(string key, bool expected)
        => Assert.That(OfflineCheckoutPolicy.IsValidKey(key), Is.EqualTo(expected));
}
