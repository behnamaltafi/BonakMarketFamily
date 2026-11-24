using Autofac;
using BonakMarket.Core.Bootstrapping;
using BonakMarket.Core.Theming;
using BonakMarket.Engine.Builders;
using BonakMarket.Web.Theming;

namespace BonakMarket.Web.Bootstrapping;

internal sealed class ThemingStarter : StarterBase
{
    public override void ConfigureContainer(ContainerBuilder builder, IApplicationContext appContext)
    {
        builder.RegisterModule(new ThemesModule());
        builder.RegisterType<ThemeVariableRepository>().AsSelf().InstancePerLifetimeScope();
        builder.RegisterType<DefaultThemeVariableService>().As<IThemeVariableService>().InstancePerLifetimeScope();
        builder.RegisterType<DefaultThemeContext>().As<IThemeContext>().InstancePerLifetimeScope();
    }

    public override void BuildPipeline(RequestPipelineBuilder builder)
    {
        var themeRegistry = builder.ApplicationBuilder.ApplicationServices.GetService<IThemeRegistry>();
        if (themeRegistry != null)
        {
            themeRegistry.ThemeExpired += OnThemeExpired;
        }
    }

    private static void OnThemeExpired(object sender, ThemeExpiredEventArgs e)
    {
        ThemeVariableRepository.RemoveFromCache(e.Cache, e.ThemeName);
    }
}