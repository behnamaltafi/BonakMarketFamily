using BonakMarket.Core.Configuration;

namespace BonakMarket.Core.Common.Configuration;

public class AdminAreaSettings : ISettings
{
    public int GridPageSize { get; set; } = 25;

    public bool DisplayProductPictures { get; set; } = true;
}