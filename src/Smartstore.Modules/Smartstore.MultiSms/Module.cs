global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Net;
global using System.Net.Http;
global using System.Net.Http.Json;
global using System.Text.Json;
global using System.Threading;
global using System.Threading.Tasks;
global using Smartstore.Web.Modelling;
using Smartstore.Engine.Modularity;
using Smartstore.Core.Widgets;
using Smartstore.MultiSms.Components;
using Smartstore.Http;
using Smartstore.MultiSms.Configuration;

namespace Smartstore.MultiSms;

internal class Module : ModuleBase, IConfigurable, IActivatableWidget
{
    public RouteInfo GetConfigurationRoute()
        => new("Configure", "MultiSms", new { area = "Admin" });

    // Smartstore's existing login form exposes this extension zone.
    // Activate the widget under Admin > CMS > Widgets.
    public Widget GetDisplayWidget(string widgetZone, object model, int storeId)
        => new ComponentWidget(typeof(SmsLoginLinkViewComponent));

    public string[] GetWidgetZones() => ["after_login_buttons"];


    public override async Task InstallAsync(ModuleInstallationContext context)
    {
        await TrySaveSettingsAsync<SmsGatewaySettings>();
        await ImportLanguageResourcesAsync();
        await base.InstallAsync(context);
    }

    public override async Task UninstallAsync()
    {
        await DeleteSettingsAsync<SmsGatewaySettings>();
        await DeleteLanguageResourcesAsync();
        await base.UninstallAsync();
    }
}
