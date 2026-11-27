using BonakMarket.Core.Catalog.Attributes;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.StorefrontApi.Models;

namespace BonakMarket.StorefrontApi.Services;

internal sealed record ProductSelectionResult(int Status, string Code = null,
    ProductVariantAttributeSelection Selection = null, List<string> Warnings = null);
