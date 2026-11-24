using BonakMarket.Web.Bundling;

namespace BonakMarket.Web.Infrastructure;

internal class PublicBundles : IBundleProvider
{
    public int Priority => 0;

    private static IEnumerable<string> ResolveThemeSourceFiles(DynamicBundleContext context)
    {
        var theme = context.RouteValues["theme"] as string;
        return [$"/Themes/{theme}/theme.scss"];
    }

    private static IEnumerable<string> ResolveRtlThemeSourceFiles(DynamicBundleContext context)
    {
        var theme = context.RouteValues["theme"] as string;
        return [$"/Themes/{theme}/theme-rtl.scss"];
    }

    private static bool IsValidTheme(DynamicBundleContext context)
    {
        var theme = context.RouteValues["theme"] as string;
        return context.ThemeRegistry.ContainsTheme(theme);
    }

    public void RegisterBundles(IApplicationContext appContext, IBundleCollection bundles)
    {
        if (!appContext.IsInstalled)
        {
            return;
        }

        var lib = "/lib/";
        var js = "/js/";


        /* Dynamic Sass themes --> /themes/[theme]/theme[-rtl].css
        -----------------------------------------------------*/
        bundles.Add(new DynamicStyleBundle("/themes/{theme}/theme.css")
            .WithConstraint(IsValidTheme)
            .Include(ResolveThemeSourceFiles));
        bundles.Add(new DynamicStyleBundle("/themes/{theme}/theme-rtl.css")
            .WithConstraint(IsValidTheme)
            .Include(ResolveRtlThemeSourceFiles));


        /* Public Common CSS --> /bundle/css/site-common.css
        -----------------------------------------------------*/
        bundles.Add(new StyleBundle("/bundle/css/site-common.css").Include(
            lib + "pnotify/css/pnotify.css",
            lib + "pnotify/css/pnotify.mobile.css",
            lib + "pnotify/css/pnotify.buttons.css"));


        /* Public Main --> /bundle/js/site.js
        -----------------------------------------------------*/
        bundles.Add(new ScriptBundle("/bundle/js/site.js").Include(
            // Vendors
            lib + "underscore/underscore.js",
            lib + "jquery/jquery.addeasing.js",
            lib + "jquery-ui/effect.js",
            lib + "jquery-ui/effect-shake.js",
            lib + "jquery/jquery.unobtrusive-ajax.js",
            lib + "jquery/jquery.validate.js",
            lib + "jquery/jquery.validate.unobtrusive.js",
            lib + "jquery/jquery.ba-outside-events.js",
            lib + "moment/moment.js",
            lib + "select2/js/select2.js",
            lib + "pnotify/js/pnotify.js",
            lib + "pnotify/js/pnotify.mobile.js",
            lib + "pnotify/js/pnotify.buttons.js",
            lib + "pnotify/js/pnotify.animate.js",
            lib + "slick/slick.js",
            lib + "aos/js/aos.js",
            lib + "popper/popper.js",
            lib + "bootstrap/js/bootstrap.js",
            // Common
            js + "underscore.mixins.js",
            js + "BonakMarket.system.js",
            js + "BonakMarket.touchevents.js",
            js + "BonakMarket.jquery.utils.js",
            js + "BonakMarket.globalization.js",
            js + "jquery.validate.unobtrusive.custom.js",
            js + "BonakMarket.viewport.js",
            js + "BonakMarket.scrollto.js",
            js + "BonakMarket.ajax.js",
            js + "BonakMarket.eventbroker.js",
            js + "BonakMarket.common.js",
            js + "BonakMarket.datetimepicker.js",
            js + "BonakMarket.globalinit.js",
            js + "BonakMarket.tabs.js",
            js + "BonakMarket.dialogs.js",
            js + "BonakMarket.selectwrapper.js",
            js + "BonakMarket.numberinput.js",
            js + "BonakMarket.throbber.js",
            js + "BonakMarket.thumbzoomer.js",
            js + "BonakMarket.articlelist.js",
            js + "BonakMarket.megamenu.js",
            js + "BonakMarket.offcanvas.js",
            js + "BonakMarket.parallax.js",
            js + "BonakMarket.media.js",
            js + "BonakMarket.cmp.js",
            js + "BonakMarket.accessibility.js",
            js + "BonakMarket.accessibility.plugins.js",
            js + "BonakMarket.accessibility.focustrap.js",
            // Shop
            js + "public.common.js",
            js + "public.search.js",
            // INFO: (mh) (core) Offcanvas cart is rendered by widget & not available on document loaded.
            //                   So it must either be rewritten so it can be initialized on request or widgets must be rendered before scripts are injected. 
            //                   The second solution might be preferable as I don't know how many other places are affected by this.
            // TODO: (mh) (core) Uncomment when this problem is solved.
            //js + "public.offcanvas-cart.js",
            js + "public.offcanvas-menu.js",
            js + "public.product.js"));
    }
}