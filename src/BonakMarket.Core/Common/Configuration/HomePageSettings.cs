using BonakMarket.Core.Configuration;
using BonakMarket.Core.Localization;

namespace BonakMarket.Core.Common.Configuration;

public class HomePageSettings : ISettings
{
    /// <summary>
    /// Gets or sets the homepage meta title.
    /// </summary>
    [LocalizedProperty]
    public string MetaTitle { get; set; }

    /// <summary>
    /// Gets or sets the homepage meta description.
    /// </summary>
    [LocalizedProperty]
    public string MetaDescription { get; set; }

    /// <summary>
    /// Gets or sets the homepage meta keywords.
    /// </summary>
    [LocalizedProperty]
    public string MetaKeywords { get; set; }
}