using Autofac;
using Microsoft.AspNetCore.Authentication;
using BonakMarket.Engine;
using BonakMarket.Engine.Builders;

namespace BonakMarket.Twitter.Auth;

internal class Startup : StarterBase
{
    public override void ConfigureContainer(ContainerBuilder builder, IApplicationContext appContext)
    {
        builder.RegisterType<TwitterOptionsConfigurer>()
            .As<IConfigureOptions<AuthenticationOptions>>()
            .As<IConfigureOptions<TwitterOptions>>()
            .InstancePerDependency();

        builder.RegisterType<TwitterPostConfigureOptions>()
            .As<IPostConfigureOptions<TwitterOptions>>()
            .InstancePerDependency();
    }
}