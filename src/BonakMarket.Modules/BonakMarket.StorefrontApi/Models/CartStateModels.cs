#nullable enable
namespace BonakMarket.StorefrontApi.Models;

public sealed record CartVariantDto(int VariantAttributeId, string? AttributeName, int ValueId, string ValueName);
public sealed record CartLinePricingDto(string CurrencyCode, decimal UnitPrice, decimal? RegularUnitPrice,
    decimal LineTotal, decimal DiscountAmount, bool TaxInclusive);
public sealed record CartLineActionsDto(bool CanUpdateQuantity, bool CanRemove, bool CanTransfer);
public sealed record CartLineDto(int Id, int ProductId, int Quantity, bool Active, int? ParentItemId,
    string? Name, string? Sku, string? ImageUrl, int? MinimumQuantity, int? MaximumQuantity,
    IReadOnlyList<int> AllowedQuantities, int? QuantityStep, string Availability,
    IReadOnlyList<CartVariantDto> Variants, CartLinePricingDto? Pricing,
    CartLineActionsDto AllowedActions, IReadOnlyList<string> Warnings);

/// <summary>Native primary currency. Subtotal already reflects product-level discounts.</summary>
public sealed record CartSummaryDto(int ItemCount, int TotalQuantity, int ActiveQuantity, string? CurrencyCode,
    decimal? SubTotal, decimal? SubTotalDiscountAmount, decimal? SubTotalAfterDiscount,
    decimal? OrderDiscountAmount, decimal? ShippingAmount, decimal? TaxAmount, bool? TaxInclusive,
    decimal? GiftCardAmount, decimal? RewardPointsAmount, int? RedeemedRewardPoints, decimal? CreditBalanceAmount,
    decimal? EstimatedTotal, decimal? PayableTotal);
public sealed record CartGiftCardDto(int Id, string MaskedCode, decimal? UsableAmount, decimal? AppliedAmount);
public sealed record CartCheckoutDto(bool CanStart, bool ShippingRequired, IReadOnlyList<string> Blockers);
public sealed record CartStateDto(string Revision, IReadOnlyList<CartLineDto> Items, CartSummaryDto Summary,
    string? AppliedCoupon, IReadOnlyList<CartGiftCardDto> AppliedGiftCards, bool UseRewardPoints,
    CartCheckoutDto Checkout, IReadOnlyList<string> Warnings);
public sealed record WishlistStateDto(string Revision, IReadOnlyList<CartLineDto> Items, int ItemCount, int TotalQuantity,
    IReadOnlyList<string> Warnings);
public sealed record CartTransferDto(CartStateDto Cart, WishlistStateDto Wishlist);
