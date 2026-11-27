#nullable enable
namespace BonakMarket.StorefrontApi.Models;

public sealed record CustomerSummaryDto(int? CartQuantity, int? WishlistQuantity, int? OrderCount,
    int? ReturnCount, int? StockSubscriptionCount, int? RewardPointsBalance, bool ProfileRequired);
