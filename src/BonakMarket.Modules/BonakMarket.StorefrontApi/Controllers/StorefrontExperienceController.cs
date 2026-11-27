using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using BonakMarket.Core.Common.Services;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core;
using BonakMarket.Core.Catalog;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Content.Media;
using BonakMarket.Core.Content.Topics;
using BonakMarket.Core.Data;
using BonakMarket.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Localization;
using BonakMarket.Core.Stores;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1")]
[StorefrontPermission(StorefrontPermissions.Catalog)]
public sealed class StorefrontExperienceController(SmartDbContext db, StorefrontRequestContext request,
    IWorkContext work, IStoreContext stores, IMediaService media,
    ShoppingCartSettings cart, OrderSettings orders, RewardPointsSettings rewards, CustomerSettings customers, ICurrencyService currencies, IConfiguration configuration) : StorefrontController
{
    [HttpGet("bootstrap"), PublicCatalog]
    public async Task<ActionResult<StorefrontBootstrapDto>> Bootstrap([FromServices] CatalogSettings catalog)
    {
        var store = stores.CurrentStore;
        var language = work.WorkingLanguage;
        var currency = work.WorkingCurrency;
        var primary = currencies.PrimaryCurrency;
        var logo = store.LogoMediaFileId > 0
            ? await media.GetUrlAsync(store.LogoMediaFileId, 400, doFallback: false) : null;
        return Ok(new StorefrontBootstrapDto(store.Id, store.Name, logo,
            new(language.Id, language.Name, language.LanguageCulture, language.Rtl ? "rtl" : "ltr"),
            new(currency.CurrencyCode, currency.RoundNumDecimals),
            new(primary.CurrencyCode, primary.RoundNumDecimals),
            new(cart.ShowDiscountBox, cart.ShowGiftCardBox, rewards.Enabled, cart.AllowActivatableCartItems,
                orders.ReturnRequestsEnabled, !customers.HideBackInStockSubscriptionsTab,
                configuration.GetValue<bool>("StorefrontApi:EnableOfflineCheckout") && db.DataProvider.ProviderType == DbSystemType.SqlServer, false)
            {
                ShowBestsellersOnHomepage = catalog.ShowBestsellersOnHomepage && catalog.NumberOfBestsellersOnHomepage > 0,
                NumberOfBestsellersOnHomepage = Math.Max(0, catalog.NumberOfBestsellersOnHomepage)
            }));
    }

    // Match public MVC visibility; password-protected topics and widget-only content are not exposed.
    private IQueryable<Topic> Topics() => StorefrontScope.Topics(db, request.Customer, request.StoreId)
        .AsNoTracking();

    [HttpGet("pages"), PublicCatalog]
    public async Task<ActionResult<PageDto<TopicSummaryDto>>> Pages([FromQuery] PageRequest page, CancellationToken ct)
    {
        var query = Topics().OrderBy(x => x.Priority).ThenBy(x => x.Id);
        return Ok(await query.PageAsync(page, rows => Task.FromResult(rows.Select(x =>
            new TopicSummaryDto(x.Id, x.SystemName,
                x.GetLocalized(y => y.Title).Value, x.GetLocalized(y => y.Intro).Value)).ToList()), ct));
    }

    [HttpGet("pages/{id:int}"), PublicCatalog]
    public async Task<ActionResult<TopicDetailDto>> Page(int id, CancellationToken ct)
    {
        var topic = await Topics().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (topic == null) return Error(404, "page_not_found");
        return Ok(new TopicDetailDto(topic.Id, topic.SystemName, topic.GetLocalized(x => x.Title).Value,
            topic.GetLocalized(x => x.Intro).Value, topic.GetLocalized(x => x.Body, detectEmptyHtml: true).Value,
            topic.GetLocalized(x => x.MetaTitle).Value, topic.GetLocalized(x => x.MetaDescription).Value));
    }
}
