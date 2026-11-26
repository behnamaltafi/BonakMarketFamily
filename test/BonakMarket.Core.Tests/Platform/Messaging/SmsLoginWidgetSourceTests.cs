using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using BonakMarket.Core.Widgets;
using BonakMarket.Engine;
using BonakMarket.Engine.Builders;
using BonakMarket.Identity.Components;
using BonakMarket.Identity.Services;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class SmsLoginWidgetSourceTests
{
    [Test]
    public async Task Enabled_sms_login_is_available_without_cms_widget_activation()
    {
        var otp = new Mock<ISmsOtpLoginService>();
        otp.Setup(x => x.IsEnabledAsync()).ReturnsAsync(true);
        var source = new SmsLoginWidgetSource(new Lazy<ISmsOtpLoginService>(() => otp.Object));

        var widgets = (await source.GetWidgetsAsync(new PlainWidgetZone("after_login_buttons"), true)).ToArray();

        Assert.That(widgets, Has.Length.EqualTo(1));
        Assert.That(((ComponentWidget)widgets[0]).ComponentType, Is.EqualTo(typeof(SmsLoginLinkViewComponent)));
    }

    [Test]
    public async Task Verified_mobile_profile_editor_is_available_on_native_customer_info_page()
    {
        var otp = new Mock<ISmsOtpLoginService>();
        otp.Setup(x => x.IsEnabledAsync()).ReturnsAsync(true);
        var source = new SmsLoginWidgetSource(new Lazy<ISmsOtpLoginService>(() => otp.Object));

        var widgets = (await source.GetWidgetsAsync(new PlainWidgetZone("customer_info_top"), true)).ToArray();

        Assert.That(widgets, Has.Length.EqualTo(1));
        Assert.That(((ComponentWidget)widgets[0]).ComponentType, Is.EqualTo(typeof(SmsProfileLinkViewComponent)));
    }

    [Test]
    public async Task Runtime_setting_changes_immediately_control_visibility()
    {
        var otp = new Mock<ISmsOtpLoginService>();
        otp.SetupSequence(x => x.IsEnabledAsync()).ReturnsAsync(true).ReturnsAsync(false);
        var source = new SmsLoginWidgetSource(new Lazy<ISmsOtpLoginService>(() => otp.Object));
        var zone = new PlainWidgetZone("after_login_buttons");

        Assert.That(await source.GetWidgetsAsync(zone, true), Is.Not.Empty);
        Assert.That(await source.GetWidgetsAsync(zone, true), Is.Empty);
    }

    [TestCase("after_login_buttons", false)]
    [TestCase("head", true)]
    [TestCase("external_auth_buttons", true)]
    public async Task Other_zones_and_admin_pages_do_not_load_otp_settings(string zoneName, bool publicArea)
    {
        var otp = new Mock<ISmsOtpLoginService>(MockBehavior.Strict);
        var lazyOtp = new Lazy<ISmsOtpLoginService>(() => otp.Object);
        var source = new SmsLoginWidgetSource(lazyOtp);

        Assert.That(await source.GetWidgetsAsync(new PlainWidgetZone(zoneName), publicArea), Is.Empty);
        Assert.That(lazyOtp.IsValueCreated, Is.False);
        otp.Verify(x => x.IsEnabledAsync(), Times.Never);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Widget_selector_can_render_layout_without_resolving_database_services(bool installed)
    {
        var appContext = new Mock<IApplicationContext>();
        appContext.SetupGet(x => x.IsInstalled).Returns(installed);
        var services = new ServiceCollection();
        var startupType = typeof(SmsLoginWidgetSource).Assembly.GetType("BonakMarket.Identity.Startup", throwOnError: true);
        var startup = (StarterBase)Activator.CreateInstance(startupType, nonPublic: true);
        startup.ConfigureServices(services, appContext.Object);
        services.AddScoped<ISmsOtpLoginService>(_ => throw new InvalidOperationException("Database services are unavailable."));
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
        services.AddScoped<IWidgetSelector, DefaultWidgetSelector>();

        var builder = new ContainerBuilder();
        builder.Populate(services);
        using var container = builder.Build();
        using var scope = container.BeginLifetimeScope();
        var selector = scope.Resolve<IWidgetSelector>();
        var widgets = new List<Widget>();

        await foreach (var widget in selector.EnumerateWidgetsAsync(new PlainWidgetZone("head")))
        {
            widgets.Add(widget);
        }

        Assert.That(widgets, Is.Empty);
        Assert.That(scope.Resolve<IEnumerable<IWidgetSource>>().Count(), Is.EqualTo(installed ? 1 : 0));
    }
}
