namespace BonakMarket.StorefrontApi.Services;

internal static class WholesaleCardMetadata
{
    internal static int? PackageSize(decimal value) =>
        value > 0m && value <= int.MaxValue && value == decimal.Truncate(value) ? (int)value : null;

    // Never infer that the sale unit is a carton from minimum quantity, quantity step or package metadata alone.
    // This is a reference display calculation, not a cart price or a new price source.
    internal static decimal? ConsumerPricePerItem(decimal retailPrice, int? unitsPerPackage, int? saleUnitId, int packageUnitId) =>
        retailPrice > 0m && unitsPerPackage is > 0 && packageUnitId > 0 && saleUnitId == packageUnitId
            ? retailPrice / unitsPerPackage.Value : null;
}
