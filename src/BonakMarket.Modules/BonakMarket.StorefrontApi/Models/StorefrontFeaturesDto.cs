#nullable enable
namespace BonakMarket.StorefrontApi.Models;

public sealed record StorefrontFeaturesDto(bool CouponEnabled, bool GiftCardEnabled, bool RewardPointsEnabled,
    bool ActivatableCartItems, bool ReturnRequestsEnabled, bool StockSubscriptionsEnabled,
    bool NativeCheckoutConfirmationEnabled, bool NativeOnlinePaymentEnabled)
{
    public bool ShowBestsellersOnHomepage { get; init; }
    public int NumberOfBestsellersOnHomepage { get; init; }
}
