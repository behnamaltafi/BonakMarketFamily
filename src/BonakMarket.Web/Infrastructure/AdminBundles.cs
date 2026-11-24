using BonakMarket.Web.Bundling;

namespace BonakMarket.Web.Infrastructure;

internal class AdminBundles : IBundleProvider
{
    public int Priority => 0;

    public void RegisterBundles(IApplicationContext appContext, IBundleCollection bundles)
    {
        /* Admin Main Sass theme --> /admin/theme[-rtl].scss
         * Also required during install.
        -----------------------------------------------------*/
        bundles.Add(new StyleBundle("/bundle/css/admin-theme.css") { IconArea = "admin" }.Include("/admin/theme.scss"));
        bundles.Add(new StyleBundle("/bundle/css/admin-theme-rtl.css") { IconArea = "admin" }.Include("/admin/theme-rtl.scss"));

        if (!appContext.IsInstalled)
        {
            return;
        }

        bundles.Add(new StyleBundle("/bundle/css/dashboard-editor.css").Include("/admin/dashboard-editor.scss"));

        var lib = "/lib/";
        var js = "/js/";
        var adminJs = "/admin/js/";
        var components = "/components/";

        /* Admin Common CSS --> /bundle/css/admin-common.css
        -----------------------------------------------------*/
        bundles.Add(new StyleBundle("/bundle/css/admin-common.css").Include(
            lib + "pnotify/css/pnotify.css",
            lib + "pnotify/css/pnotify.mobile.css",
            lib + "pnotify/css/pnotify.buttons.css"));


        /* Admin Main scripts --> /bundle/js/admin.js
        -----------------------------------------------------*/
        bundles.Add(new ScriptBundle("/bundle/js/admin.js").Include(
            adminJs + "jquery-shims.js",
            // Lib
            lib + "underscore/underscore.js",
            lib + "jquery/jquery.addeasing.js",
            lib + "jquery-ui/effect.js",
            lib + "jquery-ui/effect-transfer.js",
            lib + "jquery-ui/position.js",
            lib + "jquery/jquery.unobtrusive-ajax.js",
            lib + "jquery/jquery.validate.js",
            lib + "jquery/jquery.validate.unobtrusive.js",
            lib + "jquery/jquery.serializeToJSON.min.js",
            lib + "sortable/sortable.js",
            lib + "sortable/jquery-sortable.js",
            lib + "moment/moment.js",
            lib + "select2/js/select2.js",
            lib + "pnotify/js/pnotify.js",
            lib + "pnotify/js/pnotify.mobile.js",
            lib + "pnotify/js/pnotify.buttons.js",
            lib + "pnotify/js/pnotify.animate.js",
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
            js + "BonakMarket.throbber.js",
            js + "BonakMarket.thumbzoomer.js",
            js + "BonakMarket.entitypicker.js",
            js + "BonakMarket.rangeslider.js",
            js + "BonakMarket.numberinput.js",
            js + "BonakMarket.tree.js",
            js + "BonakMarket.media.js",
            js + "BonakMarket.colorpicker.js",
            js + "BonakMarket.editortemplates.js",
            js + "BonakMarket.offcanvas.js",
            // Admin
            adminJs + "admin.common.js",
            adminJs + "admin.layout.js",
            adminJs + "admin.media.js",
            adminJs + "admin.globalinit.js",
            adminJs + "admin.ai.js"));


        /* Chart.js --> /bundle/js/chart.js
        -----------------------------------------------------*/
        bundles.Add(new ScriptBundle("/bundle/js/chart.js").Include(
            lib + "Chart.js/Chart.js"));


        /* DataGrid --> /bundle/js/datagrid.js
        -----------------------------------------------------*/
        bundles.Add(new ScriptBundle("/bundle/js/datagrid.js").Include(
            lib + "jquery/jquery.deserialize.js",
            components + "datagrid/datagrid.js",
            components + "datagrid/datagrid-pager.js",
            components + "datagrid/datagrid-tools.js"));


        /* Summernote--> /bundle/js/summernote.js
        -----------------------------------------------------*/
        var summernote = "/lib/editors/summernote/";
        bundles.Add(new ScriptBundle("/bundle/js/summernote.js").Include(
            summernote + "summernote-sm.js",
            "/js/BonakMarket.icontools.js",
            summernote + "plugins/BonakMarket.iconpicker.js",
            summernote + "plugins/BonakMarket.iconpopover.js",
            lib + "beautify/beautify.min.js",
            lib + "beautify/beautify-css.min.js",
            lib + "beautify/beautify-html.min.js",
            summernote + "globalinit.js"));
        //var summernote = "/lib/editors/summernote_old/";
        //bundles.Add(new ScriptBundle("/bundle/js/summernote.js").Include(
        //    summernote + "summernote-bootstrap.min.js",
        //    summernote + "plugins/BonakMarket.image.js",
        //    summernote + "plugins/BonakMarket.link.js",
        //    summernote + "plugins/BonakMarket.tablestyles.js",
        //    summernote + "plugins/BonakMarket.cssclass.js",
        //    lib + "beautify/beautify.min.js",
        //    lib + "beautify/beautify-css.min.js",
        //    lib + "beautify/beautify-html.min.js",
        //    summernote + "globalinit.js"));


        /* CodeMirror (V 5.3.3) --> /bundle/js/codemirror.js
        -----------------------------------------------------*/
        var cm = "/lib/editors/CodeMirror/";
        // Script
        bundles.Add(new ScriptBundle("/bundle/js/codemirror.js").Include(
            cm + "codemirror.min.js",
            cm + "addon/fold/xml-fold.min.js",
            cm + "addon/hint/show-hint.min.js",
            cm + "addon/hint/xml-hint.min.js",
            cm + "addon/hint/html-hint.min.js",
            cm + "addon/hint/css-hint.min.js",
            cm + "addon/hint/javascript-hint.min.js",
            cm + "addon/edit/closetag.min.js",
            cm + "addon/edit/closebrackets.min.js",
            cm + "addon/edit/matchtags.min.js",
            cm + "addon/edit/matchbrackets.min.js",
            cm + "addon/mode/multiplex.min.js",
            cm + "addon/mode/overlay.min.js",
            cm + "addon/display/fullscreen.min.js",
            cm + "addon/selection/active-line.min.js",
            cm + "mode/xml/xml.min.js",
            cm + "mode/javascript/javascript.min.js",
            cm + "mode/css/css.min.js",
            cm + "mode/htmlmixed/htmlmixed.min.js",
            cm + "mode/liquid/liquid.js"));
        // CSS
        bundles.Add(new StyleBundle("/bundle/css/codemirror.css") { IconArea = "admin" }.Include(
            cm + "codemirror.min.css",
            cm + "codemirror.custom.css",
            cm + "addon/hint/show-hint.min.css",
            cm + "addon/display/fullscreen.css",
            cm + "theme/eclipse.min.css",
            cm + "mode/liquid/liquid.scss"));


        /* Roxy File Manager--> /bundle/js/filemanager.js
			------------------------------------------------------*/
        var roxy = "/lib/roxyfm/js/";
        // Script
        bundles.Add(new ScriptBundle("/bundle/js/roxyfm.js").Include(
            js + "BonakMarket.icons.js",
            roxy + "jquery-2.1.1.min.js",
            roxy + "jquery-ui-1.10.4.custom.min.js",
            roxy + "filetypes.js",
            roxy + "custom.js",
            roxy + "utils.js",
            roxy + "main.js",
            roxy + "file.js",
            roxy + "directory.js",
            roxy + "jquery-dateFormat.min.js"));
        // CSS
        bundles.Add(new StyleBundle("/bundle/css/roxyfm.css").Include("/lib/roxyfm/css/main.scss"));
    }
}