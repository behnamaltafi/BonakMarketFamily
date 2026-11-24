using BonakMarket.DevTools.Models;
using BonakMarket.Events;
using BonakMarket.Web.Rendering.Events;

namespace BonakMarket.DevTools;

public class Events : IConsumer
{
    public void HandleEvent(TabStripCreated message)
    {
        if (message.TabStripName == "product-edit")
        {
            //// Access the model
            //var productId = ((TabbableModel)message.Model).Id;

            //// Add in a widget into a dynamically created special tab called 'Plugins'
            //message.AddWidget(new ComponentWidgetInvoker(typeof(MyComponentWidget)));

            //// Add in a custom tab
            //await message.TabFactory.AddAsync(builder => builder
            //    .Text("My custom tab")
            //    .Name("tab-mycustom")
            //    .Icon("code-xml")
            //    .LinkHtmlAttributes(new { data_tab_name = "DevTools" })
            //    .Action("ProductEditTab", "DevTools", new { productId })
            //    .Ajax());
        }
    }

    public void HandleEvent(ModelBoundEvent message)
    {
        if (message.BoundModel.CustomProperties.TryGetValueAs<BackendExtensionModel>("DevTools", out var model))
        {
            // Do something with BackendExtensionModel instance
        }
    }
}