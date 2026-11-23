using BonakMarket.Web.Bundling;

namespace BonakMarket.Web.Infrastructure;

internal class CommonBundles : IBundleProvider
{
    public int Priority => 0;

    public void RegisterBundles(IApplicationContext appContext, IBundleCollection bundles)
    {
        var lib = "/lib/";
        var js = "/js/";

        /*´jQuery --> /bundle/js/jquery.js
        -----------------------------------------------------*/
        bundles.Add(new ScriptBundle("/bundle/js/jquery.js").Include(
            js + "BonakMarket.icons.js",
            lib + "jquery/jquery-3.7.1.js"));

        if (!appContext.IsInstalled)
        {
            return;
        }

        /* Vue --> /bundle/js/vue.js
        -----------------------------------------------------*/
        bundles.Add(new ScriptBundle("/bundle/js/vue.js").Include(
            lib + "vue/vue.js"));


        /* File uploader --> /bundle/js/fileuploader.js
        -----------------------------------------------------*/
        bundles.Add(new ScriptBundle("/bundle/js/fileuploader.js").Include(
            lib + "dropzone/dropzone.js",
            js + "BonakMarket.dropzoneWrapper.js"));


        /* Image Gallery --> /bundle/js/smart-gallery.js
        -----------------------------------------------------*/
        bundles.Add(new ScriptBundle("/bundle/js/smart-gallery.js").Include(
            lib + "drift/Drift.js",
            js + "BonakMarket.lightbox.js",
            js + "BonakMarket.gallery.js"));
    }
}
