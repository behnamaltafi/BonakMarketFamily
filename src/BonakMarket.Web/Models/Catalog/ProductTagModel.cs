using BonakMarket.Core.Localization;

namespace BonakMarket.Web.Models.Catalog;

public partial class ProductTagModel : EntityModelBase
{
    public LocalizedValue<string> Name { get; set; }
    public string Slug { get; set; }
    public int ProductCount { get; set; }
}