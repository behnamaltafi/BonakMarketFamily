using BonakMarket.Core.Identity;
using BonakMarket.Web.Models.Common;

namespace BonakMarket.Web.Components;

public class GdprConsentViewComponent : SmartViewComponent
{
    private readonly PrivacySettings _privacySettings;

    public GdprConsentViewComponent(PrivacySettings privacySettings)
    {
        _privacySettings = privacySettings;
    }

    public IViewComponentResult Invoke(bool isSmall)
    {
        if (!_privacySettings.DisplayGdprConsentOnForms)
        {
            return Empty();
        }

        var customer = Services.WorkContext.CurrentCustomer;

        var hasConsentedToGdpr = customer.GenericAttributes.HasConsentedToGdpr;
        if (hasConsentedToGdpr)
        {
            return Empty();
        }

        var model = new GdprConsentModel
        {
            GdprConsent = false,
            SmallDisplay = isSmall
        };

        return View(model);
    }
}