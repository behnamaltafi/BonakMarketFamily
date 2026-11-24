using System.Data;
using Autofac;
using BonakMarket.Core.Catalog.Attributes;
using BonakMarket.Core.Catalog.Categories;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Checkout.Attributes;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Common.Configuration;
using BonakMarket.Core.Content.Menus;
using BonakMarket.Core.Content.Topics;
using BonakMarket.Core.DataExchange.Export;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Localization;
using BonakMarket.Core.Localization.Routing;
using BonakMarket.Core.Logging;
using BonakMarket.Core.Messaging;
using BonakMarket.Core.Rules;
using BonakMarket.Core.Rules.Filters;
using BonakMarket.Core.Security;
using BonakMarket.Core.Stores;
using BonakMarket.Json;
using BonakMarket.Net.Mail;
using BonakMarket.Utilities.Html;
using BonakMarket.Web.Models.Common;

namespace BonakMarket.Web.Controllers;

public class HomeController : PublicController
{
    private readonly SmartDbContext _db;
    private readonly IStoreContext _storeContext;
    private readonly HomePageSettings _homePageSettings;
    private readonly IMessageFactory _messageFactory;
    private readonly PrivacySettings _privacySettings;
    private readonly CommonSettings _commonSettings;
    private readonly StoreInformationSettings _storeInformationSettings;
    private readonly ContactDataSettings _contactDataSettings;
    private readonly CompanyInformationSettings _companySettings;
    private readonly IPageAssetBuilder _assetBuilder;

    public HomeController(
        SmartDbContext db,
        IStoreContext storeContext,
        HomePageSettings homePageSettings,
        IMessageFactory messageFactory,
        PrivacySettings privacySettings,
        CommonSettings commonSettings,
        StoreInformationSettings storeInformationSettings,
        ContactDataSettings contactDataSettings,
        CompanyInformationSettings companySettings,
        IPageAssetBuilder assetBuilder)
    {
        _db = db;
        _storeContext = storeContext;
        _homePageSettings = homePageSettings;
        _messageFactory = messageFactory;
        _assetBuilder = assetBuilder;
        _assetBuilder = assetBuilder;
        _privacySettings = privacySettings;
        _commonSettings = commonSettings;
        _storeInformationSettings = storeInformationSettings;
        _contactDataSettings = contactDataSettings;
        _companySettings = companySettings;
    }

    [LocalizedRoute("/", Name = "Homepage")]
    public IActionResult Index()
    {
        var storeId = _storeContext.CurrentStore.Id;

        ViewBag.MetaTitle = _homePageSettings.GetLocalizedSetting(x => x.MetaTitle, storeId);
        ViewBag.MetaDescription = _homePageSettings.GetLocalizedSetting(x => x.MetaDescription, storeId);
        ViewBag.MetaKeywords = _homePageSettings.GetLocalizedSetting(x => x.MetaKeywords, storeId);

        BuildHomeJsonLd();

        return View();
    }

    [CheckStoreClosed(false)]
    [LocalizedRoute("/storeclosed", Name = "StoreClosed")]
    public IActionResult StoreClosed()
    {
        if (!_storeInformationSettings.StoreClosed)
        {
            return RedirectToRoute("Homepage");
        }

        return View();
    }

    [GdprConsent]
    [LocalizedRoute("/contactus", Name = "ContactUs")]
    public async Task<IActionResult> ContactUs()
    {
        var topic = await _db.Topics.AsNoTracking()
            .Where(x => x.SystemName == "ContactUs")
            .ApplyStandardFilter()
            .FirstOrDefaultAsync();

        var model = new ContactUsModel
        {
            Email = Services.WorkContext.CurrentCustomer.Email,
            FullName = Services.WorkContext.CurrentCustomer.GetFullName(),
            FullNameRequired = _privacySettings.FullNameOnContactUsRequired,
            MetaKeywords = topic?.GetLocalized(x => x.MetaKeywords),
            MetaDescription = topic?.GetLocalized(x => x.MetaDescription),
            MetaTitle = topic?.GetLocalized(x => x.MetaTitle),
        };

        return View(model);
    }

    [HttpPost, ActionName("ContactUs")]
    [ValidateCaptcha(CaptchaSettings.Targets.ContactUs)]
    [ValidateHoneypot, GdprConsent]
    [LocalizedRoute("/contactus", Name = "ContactUs")]
    public async Task<IActionResult> ContactUsSend(ContactUsModel model)
    {
        if (ModelState.IsValid)
        {
            var customer = Services.WorkContext.CurrentCustomer;
            var email = model.Email.Trim();
            var fullName = model.FullName;
            var subject = T("ContactUs.EmailSubject", Services.StoreContext.CurrentStore.Name);
            var body = HtmlUtility.ConvertPlainTextToHtml(model.Enquiry.HtmlEncode());

            // Required for some SMTP servers.
            MailAddress sender = null;
            if (!_commonSettings.UseSystemEmailForContactUsForm)
            {
                sender = new MailAddress(email, fullName);
            }

            var msg = await _messageFactory.SendContactUsMessageAsync(customer, email, fullName, subject, body, sender);

            if (msg?.Email?.Id != null)
            {
                model.SuccessfullySent = true;
                model.Result = T("ContactUs.YourEnquiryHasBeenSent");
                Services.ActivityLogger.LogActivity(KnownActivityLogTypes.PublicStoreContactUs, T("ActivityLog.PublicStore.ContactUs"));
            }
            else
            {
                ModelState.AddModelError(string.Empty, T("Common.Error.SendMail"));
                model.Result = T("Common.Error.SendMail");
            }

            return View(model);
        }

        return View(model);
    }

    public IActionResult Sitemap()
    {
        return RedirectPermanent(Services.StoreContext.CurrentStore.GetBaseUrl());
    }

    private void BuildHomeJsonLd()
    {
        var store = _storeContext.CurrentStore;
        var storeUrl = Services.WebHelper.GetStoreLocation();
        var builder = _assetBuilder.JsonLd;

        var contactPoint = JsonLdFragment.Create("ContactPoint", new
        {
            telephone = (_contactDataSettings.HotlineTelephoneNumber ?? _contactDataSettings.CompanyTelephoneNumber).NullEmpty(),
            email = _contactDataSettings.ContactEmailAddress.NullEmpty(),
            contactType = "Customer Service"
        });

        var address = JsonLdFragment.Create("PostalAddress", new
        {
            streetAddress = _companySettings.Street.NullEmpty(),
            addressLocality = _companySettings.City.NullEmpty(),
            postalCode = _companySettings.ZipCode.NullEmpty()
        });

        if (_companySettings.CountryId > 0)
        {
            var country = _db.Countries.FindById(_companySettings.CountryId, false);
            var countryCode = country?.TwoLetterIsoCode.NullEmpty();
            if (countryCode.HasValue())
            {
                contactPoint.Prop("areaServed", countryCode);
                address.Prop("addressCountry", countryCode);
            }
        }

        var searchAction = JsonLdFragment.Create("SearchAction")
            .Obj("target", JsonLdFragment.Create("EntryPoint", new
            {
                urlTemplate = storeUrl + "search?q={search_term_string}"
            }))
            .Prop("query-input", "required name=search_term_string");

        builder.Organization
            .Prop("@id", storeUrl + "#organization")
            .Prop("name", store.Name)
            .Prop("url", storeUrl)
            .Obj("contactPoint", contactPoint)
            .Obj("address", address);

        builder.WebSite
            .Prop("@id", storeUrl + "#website")
            .Prop("name", store.Name)
            .Prop("url", store.Url)
            .Obj("potentialAction", searchAction);
    }
}