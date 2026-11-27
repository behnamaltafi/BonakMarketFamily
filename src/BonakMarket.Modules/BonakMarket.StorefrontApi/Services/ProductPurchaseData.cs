using BonakMarket.StorefrontApi.Models;

namespace BonakMarket.StorefrontApi.Services;

internal sealed record ProductPurchaseData(HashSet<int> Sellable,
    Dictionary<int, (int MaxStock, bool Backorder)> CombinationCapacity,
    Dictionary<int, (bool HasVariants, bool UnsupportedRequired)> VariantInfo,
    Dictionary<int, QuantityUnitDto> Units, QuantityUnitDto DefaultUnit, bool CanCart);
