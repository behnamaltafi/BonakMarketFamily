using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Catalog;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Common;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Messaging;
using BonakMarket.Core.Security;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.Threading;
using BonakMarket.Utilities;
using BonakMarket.Utilities.Html;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1")]
[StorefrontPermission(StorefrontPermissions.Sharing)]
public sealed class SharingController(SmartDbContext db, StorefrontRequestContext request, CatalogSettings catalog,
    ShoppingCartSettings cartSettings, PrivacySettings privacy, IShoppingCartService carts,
    IMessageFactory messages, IDistributedLockProvider locks) : StorefrontController
{
    private const string QuotaKey = "StorefrontShareQuota";

    [HttpPost("products/{productId:int}/share")]
    [StorefrontPermission(StorefrontPermissions.Catalog)]
    [Consumes("application/x-www-form-urlencoded", "multipart/form-data")]
    [ValidateCaptcha(CaptchaSettings.Targets.ShareProduct), RequireCaptchaResult]
    [ProducesResponseType(202)]
    public async Task<IActionResult> Product(int productId, [FromForm] ShareRequest input, CancellationToken ct)
    {
        if (!catalog.EmailAFriendEnabled) return Error(403, "product_sharing_disabled");
        var product = await StorefrontScope.Products(db, request.Customer, request.StoreId, forDetail: true)
            .FirstOrDefaultAsync(x => x.Id == productId && !x.IsSystemProduct, ct);
        if (product == null) return Error(404, "product_not_found");
        var error = await AuthorizeSend(input, ct);
        if (error != null) return error;
        var result = await messages.SendShareProductMessageAsync(request.Customer, product, request.Customer.Email,
            input.FriendEmail.Trim(), HtmlUtility.ConvertPlainTextToHtml((input.PersonalMessage ?? "").HtmlEncode()));
        return result?.Email?.Id > 0 ? Accepted() : Error(503, "message_not_queued");
    }

    [HttpPost("wishlist/share")]
    [StorefrontPermission(StorefrontPermissions.Wishlist)]
    [StorefrontPermission(Permissions.Cart.AccessWishlist)]
    [Consumes("application/x-www-form-urlencoded", "multipart/form-data")]
    [ValidateCaptcha(CaptchaSettings.Targets.ShareWishlist), RequireCaptchaResult]
    [ProducesResponseType(202)]
    public async Task<IActionResult> Wishlist([FromForm] ShareRequest input, CancellationToken ct)
    {
        if (!cartSettings.EmailWishlistEnabled) return Error(403, "wishlist_sharing_disabled");
        var wishlist = await carts.GetCartAsync(request.Customer, ShoppingCartType.Wishlist, request.StoreId);
        if (!wishlist.HasItems) return Error(422, "wishlist_empty");
        var error = await AuthorizeSend(input, ct);
        if (error != null) return error;
        var result = await messages.SendShareWishlistMessageAsync(request.Customer, request.Customer.Email,
            input.FriendEmail.Trim(), HtmlUtility.ConvertPlainTextToHtml((input.PersonalMessage ?? "").HtmlEncode()));
        return result?.Email?.Id > 0 ? Accepted() : Error(503, "message_not_queued");
    }

    private async Task<IActionResult> AuthorizeSend(ShareRequest input, CancellationToken ct)
    {
        if (!Request.HasFormContentType || !ModelState.IsValid) return ValidationProblem(ModelState);
        if (privacy.DisplayGdprConsentOnForms && !input.GdprConsent) return Error(422, "gdpr_consent_required");
        var email = request.Customer.Email;
        if (string.IsNullOrWhiteSpace(email) || !email.IsEmail() || email.EndsWith("@accounts.invalid", StringComparison.OrdinalIgnoreCase))
            return Error(422, "email_required_for_sharing");
        // Sender is never client-supplied. Five attempts per rolling hour per customer, shared across stores and both routes.
        var keyLock = locks.GetLock($"storefront.share:{request.Customer.Id}");
        if (await keyLock.IsHeldAsync()) return Error(429, "sharing_rate_limited");
        using (await keyLock.AcquireAsync())
        {
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var row = await db.GenericAttributes.FirstOrDefaultAsync(x => x.EntityId == request.Customer.Id &&
                x.KeyGroup == nameof(Customer) && x.Key == QuotaKey && x.StoreId == 0, ct);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            List<long> timestamps;
            try { timestamps = row == null ? [] : JsonSerializer.Deserialize<List<long>>(row.Value) ?? []; }
            catch (JsonException) { return Error(503, "sharing_quota_unavailable"); }
            timestamps = timestamps.Where(x => x > now - 3600).ToList();
            if (timestamps.Count >= 5) return Error(429, "sharing_rate_limited");
            timestamps.Add(now);
            if (row == null)
            {
                row = new GenericAttribute { EntityId = request.Customer.Id, KeyGroup = nameof(Customer), Key = QuotaKey, StoreId = 0 };
                db.GenericAttributes.Add(row);
            }
            row.Value = JsonSerializer.Serialize(timestamps);
            if (input.GdprConsent) request.Customer.GenericAttributes.HasConsentedToGdpr = true;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        return null;
    }
}
