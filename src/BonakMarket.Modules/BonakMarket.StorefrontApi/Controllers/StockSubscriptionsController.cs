using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1/stock-subscriptions")]
[StorefrontPermission(StorefrontPermissions.StockSubscriptions)]
public sealed class StockSubscriptionsController(SmartDbContext db, StorefrontRequestContext request,
    IStockSubscriptionService subscriptions, CustomerSettings settings) : StorefrontController
{
    private IQueryable<BackInStockSubscription> Owned() => db.BackInStockSubscriptions.Where(x =>
        x.CustomerId == request.Customer.Id && x.StoreId == request.StoreId);

    [HttpGet]
    public async Task<ActionResult<PageDto<StockSubscriptionDto>>> Get([FromQuery] PageRequest page, CancellationToken ct)
    {
        if (settings.HideBackInStockSubscriptionsTab) return Error(403, "stock_subscriptions_disabled");
        return Ok(await Owned().AsNoTracking().OrderByDescending(x => x.CreatedOnUtc).ThenByDescending(x => x.Id)
            .Select(x => new StockSubscriptionDto(x.Id, x.ProductId, x.CreatedOnUtc)).PageAsync(page, ct));
    }

    [HttpPut("{productId:int}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Subscribe(int productId, CancellationToken ct)
    {
        if (settings.HideBackInStockSubscriptionsTab) return Error(403, "stock_subscriptions_disabled");
        // Native notifications are email-based; do not register undeliverable placeholder addresses.
        if (string.IsNullOrWhiteSpace(request.Customer.Email) || request.Customer.Email.EndsWith("@accounts.invalid", StringComparison.OrdinalIgnoreCase))
            return Error(422, "email_required_for_stock_notification");
        var product = await StorefrontScope.Products(db, request.Customer, request.StoreId, forDetail: true)
            .FirstOrDefaultAsync(x => x.Id == productId && !x.IsSystemProduct, ct);
        if (product == null) return Error(404, "product_not_found");
        if (await subscriptions.IsSubscribedAsync(product, request.Customer, request.StoreId)) return NoContent();
        var (success, message) = await subscriptions.SubscribeAsync(product, request.Customer, request.StoreId);
        return success ? NoContent() : Problem(statusCode: 422, title: "subscription_not_allowed", detail: message);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var subscription = await Owned().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (subscription == null) return Error(404, "subscription_not_found");
        db.BackInStockSubscriptions.Remove(subscription);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
