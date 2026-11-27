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
