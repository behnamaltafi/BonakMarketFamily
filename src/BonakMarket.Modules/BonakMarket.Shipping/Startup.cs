using Microsoft.Extensions.DependencyInjection;
using BonakMarket.Core.Data;
using BonakMarket.Data;
using BonakMarket.Data.Providers;
using BonakMarket.Engine;
using BonakMarket.Engine.Builders;

namespace BonakMarket.Shipping;

internal class Startup : StarterBase
{
    public override void ConfigureServices(IServiceCollection services, IApplicationContext appContext)
    {
        services.AddTransient<IDbContextConfigurationSource<SmartDbContext>, SmartDbContextConfigurer>();
    }

    class SmartDbContextConfigurer : IDbContextConfigurationSource<SmartDbContext>
    {
        public void Configure(IServiceProvider services, DbContextOptionsBuilder builder)
        {
            builder.UseDbFactory(b =>
            {
                b.AddModelAssembly(this.GetType().Assembly);
            });
        }
    }
}