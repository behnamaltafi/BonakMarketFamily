using BonakMarket.Core.Configuration;
using BonakMarket.Core.Stores;
using BonakMarket.Engine.Modularity;
using BonakMarket.Http;
using BonakMarket.OfflinePayment.Settings;

namespace BonakMarket.OfflinePayment;

[SystemName("Payments.CashOnDelivery")]
[FriendlyName("Cash On Delivery (COD)")]
[Icon("money")]
[Order(100)]
public class CashOnDeliveryProvider : OfflinePaymentProviderBase<CashOnDeliveryPaymentSettings>, IConfigurable
{
    public CashOnDeliveryProvider(
        IStoreContext storeContext,
        ISettingFactory settingFactory)
        : base(storeContext, settingFactory)
    {
    }

    public RouteInfo GetConfigurationRoute()
        => new("CashOnDeliveryConfigure", "OfflinePayment", new { area = "Admin" });
}