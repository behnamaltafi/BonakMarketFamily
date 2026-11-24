using Microsoft.AspNetCore.Mvc.Rendering;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Localization;

namespace BonakMarket.Web.Models.Catalog;

public interface IQuantityInput
{
    int EnteredQuantity { get; set; }
    int MinOrderAmount { get; set; }
    int MaxOrderAmount { get; set; }
    int QuantityStep { get; set; }
    int? MaxInStock { get; set; }
    LocalizedValue<string> QuantityUnitName { get; set; }
    LocalizedValue<string> QuantityUnitNamePlural { get; set; }
    List<SelectListItem> AllowedQuantities { get; }
    QuantityControlType QuantityControlType { get; set; }
}