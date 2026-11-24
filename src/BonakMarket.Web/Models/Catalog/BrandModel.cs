using BonakMarket.Core.Catalog.Search;
using BonakMarket.Core.Localization;
using BonakMarket.Web.Models.Common;
using BonakMarket.Web.Models.Media;
using BonakMarket.Web.Models.Search;

namespace BonakMarket.Web.Models.Catalog;

public partial class BrandModel : EntityModelBase, ISearchResultModel
{
    public CatalogSearchResult SearchResult { get; set; }

    public LocalizedValue<string> Name { get; set; }
    public LocalizedValue<string> Description { get; set; }
    public LocalizedValue<string> BottomDescription { get; set; }
    public LocalizedValue<string> MetaKeywords { get; set; }
    public LocalizedValue<string> MetaDescription { get; set; }
    public LocalizedValue<string> MetaTitle { get; set; }

    public string SeName { get; set; }
    public string Path { get; set; }
    public string CanonicalUrl { get; set; }
    public ImageModel Image { get; set; } = new();

    public ProductSummaryModel FeaturedProducts { get; set; }
    public ProductSummaryModel Products { get; set; }
    public MetaPropertiesModel MetaProperties { get; set; } = new();
}