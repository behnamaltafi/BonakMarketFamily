#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

/// <summary>Extends the existing product summary wire contract with detail-only fields.</summary>
public sealed class ProductDetailDto : ProductDto
{
    public string? FullDescriptionHtml { get; set; }
    public int ProductTypeId { get; set; }
    public IReadOnlyList<ProductImageDto> Images { get; set; } = [];
    public IReadOnlyList<ProductDetailVariantDto> Variants { get; set; } = [];
    public IReadOnlyList<ProductSpecificationDto> Specifications { get; set; } = [];
    public ProductPurchaseRulesDto PurchaseRules { get; set; } = null!;
    public ProductCapabilitiesDto AllowedActions { get; set; } = null!;
    public bool RequiresShipping { get; set; }
    public int ReviewCount { get; set; }
    public decimal? AverageRating { get; set; }
}
public sealed record ProductImageDto(int MediaFileId, string? Url);
public sealed record ProductSpecificationDto(int AttributeId, string Name, int OptionId, string Value);
public sealed record ProductDetailVariantDto(int Id, string Name, int ControlTypeId, bool Required,
    string? Prompt, bool SupportsSelection, IReadOnlyList<ProductDetailVariantValueDto> Values);
public sealed record ProductDetailVariantValueDto(int Id, string Name, bool IsPreselected, string? Color);
public sealed record ProductPurchaseRulesDto(int MinimumQuantity, int MaximumQuantity,
    int QuantityStep, IReadOnlyList<int> AllowedQuantities, bool HideQuantityControl, bool RequiresConfiguration);
/// <summary>Capabilities, not a stock reservation or a guarantee the cart will accept a selection.</summary>
public sealed record ProductCapabilitiesDto(bool CanQuote, bool CanAddToCart, bool CanAddToWishlist);

public sealed class ProductQuoteRequest
{
    [Range(1, StorefrontLimits.MaxQuantity)] public int Quantity { get; set; } = 1;
    [MaxLength(StorefrontLimits.MaxVariantChoices)] public List<VariantChoice>? Variants { get; set; } = [];
}
public sealed record ProductQuotePricingDto(string CurrencyCode, decimal UnitPrice, decimal? RegularUnitPrice,
    decimal LineTotal, decimal DiscountAmount, bool TaxInclusive);
public sealed record ProductQuoteDto(int ProductId, int Quantity, bool IsValid, string? Code,
    ProductQuotePricingDto? Pricing, IReadOnlyList<string> Warnings, bool RequiresCartValidation);
