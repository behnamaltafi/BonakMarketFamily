using Autofac;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using BonakMarket.Engine;
using BonakMarket.Engine.Builders;

namespace BonakMarket.Microsoft.Auth;

internal class Startup : StarterBase
{
    public override void ConfigureContainer(ContainerBuilder builder, IApplicationContext appContext)
    {
        builder.RegisterType<MicrosoftAccountOptionsConfigurer>()
            .As<IConfigureOptions<AuthenticationOptions>>()
            .As<IConfigureOptions<MicrosoftAccountOptions>>()
            .InstancePerDependency();

        builder.RegisterType<OAuthPostConfigureOptions<MicrosoftAccountOptions, MicrosoftAccountHandler>>()
            .As<IPostConfigureOptions<MicrosoftAccountOptions>>()
            .InstancePerDependency();
    }
}