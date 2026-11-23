using BonakMarket.Core.Catalog.Search;
using BonakMarket.Core.Localization;
using BonakMarket.Web.Models.Search;

namespace BonakMarket.Web.Models.Catalog;

public partial class ProductsByTagModel : EntityModelBase, ISearchResultModel
{
    public CatalogSearchResult SearchResult { get; set; }
    public LocalizedValue<string> TagName { get; set; }
    public ProductSummaryModel Products { get; set; }
    public string CanonicalUrl { get; set; }
}