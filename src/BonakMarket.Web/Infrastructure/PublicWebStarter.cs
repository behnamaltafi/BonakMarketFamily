using Autofac;
using BonakMarket.Engine.Builders;

namespace BonakMarket.Web.Infrastructure;

internal class PublicWebStarter : StarterBase
{
    public override void ConfigureContainer(ContainerBuilder builder, IApplicationContext appContext)
    {
        if (appContext.IsInstalled)
        {
            builder.RegisterType<CatalogHelper>().InstancePerLifetimeScope();
            builder.RegisterType<OrderHelper>().InstancePerLifetimeScope();
        }
    }
}