using BonakMarket.Core.Configuration;
using BonakMarket.Core.Stores;
using BonakMarket.Engine.Modularity;
using BonakMarket.Http;
using BonakMarket.OfflinePayment.Settings;

namespace BonakMarket.OfflinePayment;

[SystemName("Payments.Prepayment")]
[FriendlyName("Prepayment")]
[Order(100)]
public class PrepaymentProvider : OfflinePaymentProviderBase<PrepaymentPaymentSettings>, IConfigurable
{
    public PrepaymentProvider(
        IStoreContext storeContext,
        ISettingFactory settingFactory)
        : base(storeContext, settingFactory)
    {
    }

    public RouteInfo GetConfigurationRoute()
        => new("PrepaymentConfigure", "OfflinePayment", new { area = "Admin" });
}