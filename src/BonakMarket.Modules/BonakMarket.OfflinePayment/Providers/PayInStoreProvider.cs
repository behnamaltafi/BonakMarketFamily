using BonakMarket.Core.Configuration;
using BonakMarket.Core.Stores;
using BonakMarket.Engine.Modularity;
using BonakMarket.Http;
using BonakMarket.OfflinePayment.Settings;

namespace BonakMarket.OfflinePayment;

[SystemName("Payments.PayInStore")]
[FriendlyName("Pay In Store")]
[Order(100)]
public class PayInStoreProvider : OfflinePaymentProviderBase<PayInStorePaymentSettings>, IConfigurable
{
    public PayInStoreProvider(
        IStoreContext storeContext,
        ISettingFactory settingFactory)
        : base(storeContext, settingFactory)
    {
    }

    public RouteInfo GetConfigurationRoute()
        => new("PayInStoreConfigure", "OfflinePayment", new { area = "Admin" });
}