using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using BonakMarket.Core.Configuration;
using BonakMarket.StorefrontApi.Configuration;
using BonakMarket.StorefrontApi.Controllers;
using BonakMarket.Web.Modelling.Settings;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class WholesaleCardAdminTests
{
    [Test]
    public void SettingsAreNativeAndDisabledByDefault()
    {
        var settings = new WholesaleCardSettings();
        Assert.That(settings, Is.InstanceOf<ISettings>());
        Assert.That(settings.UnitsPerPackageSpecificationId, Is.Zero);
        Assert.That(settings.PackageQuantityUnitId, Is.Zero);
    }

    [Test]
    public void ConventionalAdminControllerDoesNotOptIntoNativeApiExplorer()
    {
        // The native ApiControllerModelConvention uses attribute presence (not IgnoreApi)
        // to opt a controller into ApiExplorer. Conventional admin routes must never opt in.
        var controller = typeof(StorefrontApiAdminController);
        Assert.That(controller.IsDefined(typeof(ApiExplorerSettingsAttribute), inherit: true), Is.False);
        Assert.That(controller.IsDefined(typeof(RouteAttribute), inherit: true), Is.False);
        Assert.That(typeof(StorefrontController).IsDefined(typeof(ApiExplorerSettingsAttribute), inherit: true), Is.True);

        foreach (var action in controller.GetMethods().Where(x => x.Name == "Configure"))
            Assert.That(action.IsDefined(typeof(ApiExplorerSettingsAttribute), inherit: true), Is.False);
    }

    [Test]
    public void AdminActionsUseGlobalNativeSettingsAndAntiforgery()
    {
        var actions = typeof(StorefrontApiAdminController).GetMethods().Where(x => x.Name == "Configure").ToArray();
        var get = actions.Single(x => x.IsDefined(typeof(HttpGetAttribute), true));
        var post = actions.Single(x => x.IsDefined(typeof(HttpPostAttribute), true));
        Assert.That(get.GetCustomAttribute<LoadSettingAttribute>().HasStoreScope, Is.False);
        Assert.That(post.GetCustomAttribute<SaveSettingAttribute>().HasStoreScope, Is.False);
        Assert.That(post.IsDefined(typeof(ValidateAntiForgeryTokenAttribute), true), Is.True);
    }
}
