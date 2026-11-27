using BonakMarket.Core.Configuration;

namespace BonakMarket.StorefrontApi.Configuration;

/// <summary>Global mapping of native packaging specifications and sellable package unit.</summary>
public sealed class WholesaleCardSettings : ISettings
{
    /// <summary>Numeric product specification holding the number of individual items per package.</summary>
    public int UnitsPerPackageSpecificationId { get; set; }

    /// <summary>Native quantity unit that explicitly represents a package. Zero disables per-item derivation.</summary>
    public int PackageQuantityUnitId { get; set; }
}
