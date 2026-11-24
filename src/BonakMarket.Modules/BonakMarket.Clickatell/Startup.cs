using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using BonakMarket.Clickatell.Services;
using BonakMarket.Engine;
using BonakMarket.Engine.Builders;
using BonakMarket.Net.Http;

namespace BonakMarket.Clickatell;

internal class Startup : StarterBase
{
    public override void ConfigureServices(IServiceCollection services, IApplicationContext appContext)
    {
        services.AddHttpClient<ClickatellHttpClient>()
            .AddBonakMarketUserAgent()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://platform.clickatell.com/messages");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            });
    }
}