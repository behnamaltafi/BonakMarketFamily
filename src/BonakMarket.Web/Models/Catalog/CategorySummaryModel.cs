using BonakMarket.Core.Localization;
using BonakMarket.Web.Models.Media;

namespace BonakMarket.Web.Models.Catalog;

public partial class CategorySummaryModel : EntityModelBase
{
    public LocalizedValue<string> Name { get; set; }
    public string Url { get; set; }
    public ImageModel Image { get; set; } = new ImageModel();

    // TODO: Badges
}