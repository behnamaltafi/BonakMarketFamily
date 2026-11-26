namespace BonakMarket.StorefrontApi.Services;

/// <summary>Only audited native offline implementations; no arbitrary provider can opt in by its capability flags alone.</summary>
public static class OfflineCheckoutPolicy
{
    public static bool IsSupported(string systemName, string implementation, string moduleName) =>
        moduleName == "BonakMarket.OfflinePayment" && ((systemName, implementation) switch
        {
            ("Payments.CashOnDelivery", "BonakMarket.OfflinePayment.CashOnDeliveryProvider") => true,
            ("Payments.Prepayment", "BonakMarket.OfflinePayment.PrepaymentProvider") => true,
            _ => false
        });

    public static bool IsValidKey(string key) =>
        Guid.TryParseExact(key, "D", out var id) && id != Guid.Empty;
}
