using BonakMarket.Core.Configuration;

namespace BonakMarket.StorefrontApi.Configuration;

/// <summary>Global mapping of native product specifications to wholesale display metadata.</summary>
public sealed class WholesaleCardSettings : ISettings
{
    public int UnitsPerPackageSpecificationId { get; set; }
    public int ConsumerPriceSpecificationId { get; set; }
    public string ConsumerPriceCurrencyCode { get; set; } = string.Empty;
}
