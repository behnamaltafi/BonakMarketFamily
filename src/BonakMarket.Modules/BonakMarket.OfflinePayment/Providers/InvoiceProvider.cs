using BonakMarket.Core.Configuration;
using BonakMarket.Core.Stores;
using BonakMarket.Engine.Modularity;
using BonakMarket.Http;
using BonakMarket.OfflinePayment.Settings;

namespace BonakMarket.OfflinePayment;

[SystemName("Payments.Invoice")]
[FriendlyName("Invoice")]
[Order(100)]
public class InvoiceProvider : OfflinePaymentProviderBase<InvoicePaymentSettings>, IConfigurable
{
    public InvoiceProvider(
        IStoreContext storeContext,
        ISettingFactory settingFactory)
        : base(storeContext, settingFactory)
    {
    }

    public RouteInfo GetConfigurationRoute()
        => new("InvoiceConfigure", "OfflinePayment", new { area = "Admin" });
}