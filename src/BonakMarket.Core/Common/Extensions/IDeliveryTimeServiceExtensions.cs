using BonakMarket.Core.Catalog;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Common;
using BonakMarket.Core.Common.Services;

namespace BonakMarket;

public static class IDeliveryTimeServiceExtensions
{
    /// <summary>
    /// Gets the product delivery time according to stock.
    /// </summary>
    public static Task<DeliveryTime> GetDeliveryTimeAsync(this IDeliveryTimeService service, Product product, CatalogSettings catalogSettings)
    {
        var deliveryTimeId = product.GetDeliveryTimeIdAccordingToStock(catalogSettings);
        return service.GetDeliveryTimeAsync(deliveryTimeId, false);
    }
}