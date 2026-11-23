using BonakMarket.Core.Configuration;

namespace BonakMarket.Core.Common.Configuration;

public class MeasureSettings : ISettings
{
    public int BaseDimensionId { get; set; }
    public int BaseWeightId { get; set; }
}