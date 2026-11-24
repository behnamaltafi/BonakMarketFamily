using BonakMarket.Core.Localization;
using BonakMarket.Web.Models.Media;

namespace BonakMarket.Web.Models.Catalog;

public partial class BrandNavigationModel : ModelBase
{
    public List<BrandBriefInfoModel> Brands { get; set; } = new();
    public bool DisplayAllBrandsLink { get; set; }
    public bool DisplayBrands { get; set; }
    public bool DisplayImages { get; set; }
    public bool HideBrandDefaultPictures { get; set; }
    public int BrandThumbImageSize { get; set; }
}

public partial class BrandBriefInfoModel : EntityModelBase
{
    public LocalizedValue<string> Name { get; set; }
    public string SeName { get; set; }
    public int DisplayOrder { get; set; }
    public ImageModel Image { get; set; }
}