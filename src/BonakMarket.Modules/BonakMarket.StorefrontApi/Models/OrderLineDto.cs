#nullable restore
using System.ComponentModel.DataAnnotations;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Models;

public sealed record OrderLineDto(int Id, int ProductId, int Quantity, decimal UnitPriceInclTax, decimal PriceInclTax)
{
    public string Sku { get; init; }
    public string CurrentProductName { get; init; }
    public string CurrentImageUrl { get; init; }
    public string AttributeDescriptionHtml { get; init; }
    public decimal UnitPriceExclTax { get; init; }
    public decimal PriceExclTax { get; init; }
    public decimal DiscountAmountInclTax { get; init; }
    public decimal DiscountAmountExclTax { get; init; }
    public decimal TaxRate { get; init; }
}
