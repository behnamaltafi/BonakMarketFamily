#nullable enable
namespace BonakMarket.StorefrontApi.Models;

public sealed record CartLineDto(int Id, int ProductId, int Quantity, bool Active, int? ParentItemId,
    string? Name, string? Sku, string? ImageUrl, int? MinimumQuantity, int? MaximumQuantity,
    IReadOnlyList<int> AllowedQuantities, int? QuantityStep, string Availability,
    IReadOnlyList<CartVariantDto> Variants, CartLinePricingDto? Pricing,
    CartLineActionsDto AllowedActions, IReadOnlyList<string> Warnings);
