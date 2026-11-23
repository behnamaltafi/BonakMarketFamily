using Microsoft.AspNetCore.Mvc.Rendering;

namespace BonakMarket.Admin.Models;

public partial class StoreScopeConfigurationModel
{
    public int StoreId { get; set; }
    public List<SelectListItem> AllStores { get; set; } = new();
}