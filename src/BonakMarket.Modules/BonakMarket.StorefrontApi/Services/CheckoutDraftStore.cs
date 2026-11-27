#nullable enable
using System.Text.Json;
using BonakMarket.Core.Common;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.Threading;

namespace BonakMarket.StorefrontApi.Services;

internal sealed class CheckoutDraftStore(SmartDbContext db, StorefrontRequestContext request, IDistributedLockProvider locks)
{
    private const string DraftKey = "BonakMarket.Storefront.CheckoutDraft.v1";

    internal CheckoutDraft? Read()
    {
        var value = request.Customer.GenericAttributes.Get<string>(DraftKey, request.StoreId);
        if (string.IsNullOrEmpty(value)) return null;
        try { return JsonSerializer.Deserialize<CheckoutDraft>(value); }
        catch (JsonException) { return null; }
    }

    internal async Task SaveAsync(CheckoutDraft draft, CancellationToken ct)
    {
        request.Customer.GenericAttributes.Set(DraftKey, JsonSerializer.Serialize(draft), request.StoreId);
        await db.SaveChangesAsync(ct);
    }

    internal IDistributedLock Lock() => locks.GetLock($"storefront.checkout-draft:{request.StoreId}:{request.Customer.Id}");

    internal async Task ClearAsync(CancellationToken ct)
    {
        request.Customer.GenericAttributes.Set<string>(DraftKey, null, request.StoreId);
        await db.SaveChangesAsync(ct);
    }
}
