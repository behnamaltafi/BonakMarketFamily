using BonakMarket.Core.Localization;
using BonakMarket.Web.Models.Media;

namespace BonakMarket.Web.Models.Catalog;

public partial class BrandOverviewModel : EntityModelBase
{
    public LocalizedValue<string> Name { get; set; }
    public LocalizedValue<string> Description { get; set; }
    public string SeName { get; set; }
    public ImageModel Image { get; set; }
}