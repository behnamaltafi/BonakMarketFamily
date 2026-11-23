using BonakMarket.Collections;
using BonakMarket.Core.Catalog.Products;

namespace BonakMarket.Web.Models.Catalog;

public partial class GroupedProductModel : EntityModelBase
{
    public string AssociatedProductsTitle { get; set; }
    public GroupedProductConfiguration Configuration { get; set; }
    public IPagedList<ProductDetailsModel> Products { get; set; }
}