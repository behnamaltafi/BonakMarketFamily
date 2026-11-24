using Autofac;
using BonakMarket.Core.Configuration;
using BonakMarket.Engine.Builders;

namespace BonakMarket.Core.Bootstrapping;

internal sealed class SettingsStarter : StarterBase
{
    public override void ConfigureContainer(ContainerBuilder builder, IApplicationContext appContext)
    {
        builder.RegisterSource(new SettingsSource());

        builder.RegisterType<SettingFactory>().As<ISettingFactory>().SingleInstance();
        builder.RegisterType<SettingService>().As<ISettingService>().InstancePerLifetimeScope();
    }
}