using Autofac;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using BonakMarket.Engine;
using BonakMarket.Engine.Builders;

namespace BonakMarket.Facebook.Auth;

internal class Startup : StarterBase
{
    public override void ConfigureContainer(ContainerBuilder builder, IApplicationContext appContext)
    {
        builder.RegisterType<FacebookOptionsConfigurer>()
            .As<IConfigureOptions<AuthenticationOptions>>()
            .As<IConfigureOptions<FacebookOptions>>()
            .InstancePerDependency();

        builder.RegisterType<OAuthPostConfigureOptions<FacebookOptions, FacebookHandler>>()
            .As<IPostConfigureOptions<FacebookOptions>>()
            .InstancePerDependency();
    }
}