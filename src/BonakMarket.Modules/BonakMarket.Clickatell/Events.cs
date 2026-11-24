using BonakMarket.Clickatell.Services;
using BonakMarket.Clickatell.Settings;
using BonakMarket.Core;
using BonakMarket.Core.Checkout.Orders.Events;
using BonakMarket.Core.Localization;
using BonakMarket.Engine.Modularity;
using BonakMarket.Events;

namespace BonakMarket.Clickatell;

public class Events : IConsumer
{
    public Localizer T { get; set; } = NullLocalizer.Instance;

    public async Task HandleEventAsync(OrderPlacedEvent message,
        ClickatellHttpClient client,
        ICommonServices services,
        ClickatellSettings clickatellSettings,
        IProviderManager providerManager,
        CancellationToken cancelToken)
    {
        if (!clickatellSettings.Enabled)
        {
            return;
        }

        var module = services.ApplicationContext.ModuleCatalog.GetModuleByAssembly(typeof(Events).Assembly);

        await client.SendSmsAsync(
            T("Plugins.Sms.Clickatell.OrderPlacedMessage", message.Order.GetOrderNumber()),
            clickatellSettings,
            cancelToken);

        services.DbContext.OrderNotes.Add(message.Order, T("Plugins.Sms.Clickatell.SmsSentNote"));

        await services.DbContext.SaveChangesAsync(cancelToken);
    }
}