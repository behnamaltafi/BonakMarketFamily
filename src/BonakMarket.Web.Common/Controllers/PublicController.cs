using BonakMarket.Core.Checkout.Affiliates;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Security;
using BonakMarket.Core.Stores;
using BonakMarket.Web.Theming;

namespace BonakMarket.Web.Controllers;

[CookieConsent]
[PreviewMode]
[CheckStoreClosed]
[AuthorizeShopAccess]
[TrackActivity(Order = 100)]
[CheckAffiliate(Order = 100)]
[SaveChanges<SmartDbContext>(Order = int.MaxValue)]
public class PublicController : SmartController
{
}