#nullable enable
using System.Text.Json;
using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BonakMarket.Core.Checkout.Attributes;
using BonakMarket.Core.Checkout.Cart.Events;
using BonakMarket.Core.Stores;
using BonakMarket.Events;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Checkout.Payment;
using BonakMarket.Core.Checkout.Shipping;
using BonakMarket.Core.Common;
using BonakMarket.Core.Common.Services;
using BonakMarket.Core.Data;
using BonakMarket.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Security;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.Threading;

namespace BonakMarket.StorefrontApi.Services;

/// <summary>Customer/store-scoped checkout preparation and opt-in native offline confirmation.</summary>





public sealed class CheckoutSessionService(SmartDbContext db, StorefrontRequestContext request,
    CartFacade cartFacade, IShoppingCartService carts, IShippingService shipping, IConfiguration configuration,
    IOrderCalculationService calculations, IOrderProcessingService processing, IStoreContext stores,
    ICurrencyService currencies, IEventPublisher events, ILogger<CheckoutSessionService> logger,
    CheckoutDraftStore drafts, CheckoutSessionState sessionState, CheckoutOptions checkoutOptions,
    CheckoutAddressChecks addressChecks, OfflineCheckoutEligibility offlineEligibility)
{
    private const int RevisionConflict = 409;
    private const string AttemptKey = "BonakMarket.Storefront.OrderAttempt.v1";
    private bool OfflineEnabled => configuration.GetValue<bool>("StorefrontApi:EnableOfflineCheckout") && db.DataProvider.ProviderType == DbSystemType.SqlServer;
    private Task<ShoppingCart> NativeCart() => carts.GetCartAsync(request.Customer, ShoppingCartType.ShoppingCart, request.StoreId);

    /// <summary>Creates or resumes the single current draft for this customer/store.</summary>

    public async Task<StorefrontResult<CheckoutSessionDto>> StartAsync(Func<string> checkoutUrl, CancellationToken ct)
    {
        await using var handle = await drafts.Lock().AcquireAsync(TimeSpan.FromSeconds(10), ct);
        await request.Customer.GenericAttributes.ReloadAsync();
        var state = await cartFacade.StateAsync(ct);
        if (!state.Checkout.CanStart)
        {
            return new StorefrontFailure(422, "checkout_not_ready", new Dictionary<string, object?>
            {
                ["blockers"] = state.Checkout.Blockers,
                ["warnings"] = state.Warnings
            });
        }

        var native = await NativeCart();
        var draft = drafts.Read();
        if (draft == null || draft.ExpiresAt <= DateTime.UtcNow || !await addressChecks.AddressesCurrent(draft, ct) ||
            (draft.PreparedCartFingerprint == null ? !draft.IsCurrent(state.Revision, DateTime.UtcNow) :
                draft.PreparedCartFingerprint != await sessionState.RawCartFingerprint(ct) || !sessionState.NativeSelectionsMatch(draft)))
        {
            draft = CheckoutDraft.Create(state.Revision, DateTime.UtcNow);
            await drafts.SaveAsync(draft, ct);
        }

        return new(await ToDto(draft, state, native, checkoutUrl, ct));
    }

    public async Task<StorefrontResult<CheckoutSessionDto>> CurrentAsync(Func<string> checkoutUrl, CancellationToken ct)
    {
        var draft = drafts.Read();
        if (draft == null) return new StorefrontFailure(404, "checkout_session_not_found");
        return new(await ToDto(draft, await cartFacade.StateAsync(ct), await NativeCart(), checkoutUrl, ct));
    }

    public async Task<StorefrontResult<CheckoutSessionDto>> GetAsync(Guid id, Func<string> checkoutUrl, CancellationToken ct)
    {
        var draft = drafts.Read();
        if (draft == null || draft.Id != id) return new StorefrontFailure(404, "checkout_session_not_found");
        return new(await ToDto(draft, await cartFacade.StateAsync(ct), await NativeCart(), checkoutUrl, ct));
    }

    public async Task<StorefrontResult<CheckoutSessionDto>> SetAddressesAsync(Guid id, CheckoutAddressesRequest input, Func<string> checkoutUrl, CancellationToken ct)
    {
        await using var handle = await drafts.Lock().AcquireAsync(TimeSpan.FromSeconds(10), ct);
        await request.Customer.GenericAttributes.ReloadAsync();
        var loaded = await LoadCurrent(id, input.Revision, ct);
        if (loaded.Error != null) return loaded.Error;
        var draft = loaded.Draft!;
        var native = loaded.Native!;
        var needsBilling = native.Requirements.HasFlag(CheckoutRequirements.BillingAddress);
        if (needsBilling != input.BillingAddressId.HasValue || native.IsShippingRequired != input.ShippingAddressId.HasValue)
            return new StorefrontFailure(422, "checkout_addresses_required");

        var billing = input.BillingAddressId.HasValue ? await addressChecks.OwnedAddress(input.BillingAddressId.Value, ct) : null;
        var delivery = input.ShippingAddressId.HasValue ? await addressChecks.OwnedAddress(input.ShippingAddressId.Value, ct) : null;
        if ((input.BillingAddressId.HasValue && billing == null) || (input.ShippingAddressId.HasValue && delivery == null))
            return new StorefrontFailure(404, "address_not_found");
        if ((billing != null && !await addressChecks.ValidAddress(billing, false)) || (delivery != null && !await addressChecks.ValidAddress(delivery, true)))
            return new StorefrontFailure(422, "invalid_checkout_address");

        var next = draft.WithAddresses(billing?.Id, delivery?.Id, CheckoutAddressChecks.AddressFingerprint(billing), CheckoutAddressChecks.AddressFingerprint(delivery)).Advance();
        await drafts.SaveAsync(next, ct);
        return new(await ToDto(next, loaded.Cart!, native, checkoutUrl, ct));
    }

    public async Task<StorefrontResult<IReadOnlyList<CheckoutShippingOptionDto>>> ShippingOptionsAsync(Guid id, CancellationToken ct)
    {
        var loaded = await LoadCurrent(id, null, ct);
        if (loaded.Error != null) return loaded.Error;
        var draft = loaded.Draft!;
        var native = loaded.Native!;
        if (!native.IsShippingRequired) return new(Array.Empty<CheckoutShippingOptionDto>());
        if (!CheckoutSessionState.AddressesComplete(draft, native)) return new StorefrontFailure(422, "checkout_addresses_required");
        var options = await checkoutOptions.QuoteShipping(native, draft.ShippingAddressId!.Value, ct);
        if (options == null) return new StorefrontFailure(422, "shipping_unavailable");
        return new(options);
    }

    public async Task<StorefrontResult<CheckoutSessionDto>> SetShippingAsync(Guid id, CheckoutShippingSelectionRequest input, Func<string> checkoutUrl, CancellationToken ct)
    {
        await using var handle = await drafts.Lock().AcquireAsync(TimeSpan.FromSeconds(10), ct);
        await request.Customer.GenericAttributes.ReloadAsync();
        var loaded = await LoadCurrent(id, input.Revision, ct);
        if (loaded.Error != null) return loaded.Error;
        var draft = loaded.Draft!;
        var native = loaded.Native!;
        if (!native.IsShippingRequired) return new StorefrontFailure(422, "shipping_not_required");
        if (!CheckoutSessionState.AddressesComplete(draft, native)) return new StorefrontFailure(422, "checkout_addresses_required");
        var options = await checkoutOptions.QuoteShipping(native, draft.ShippingAddressId!.Value, ct);
        var matches = options?.Where(x => x.Id == input.ShippingMethodId &&
            string.Equals(x.Provider, input.Provider, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches?.Count != 1) return new StorefrontFailure(422, "shipping_option_unavailable");
        var option = matches[0];
        if (input.ObservedRate != option.Rate) return new StorefrontFailure(409, "shipping_rate_changed");

        var next = draft.WithShipping(option).Advance();
        await drafts.SaveAsync(next, ct);
        return new(await ToDto(next, loaded.Cart!, native, checkoutUrl, ct));
    }

    public async Task<StorefrontResult<IReadOnlyList<CheckoutPaymentOptionDto>>> PaymentMethodsAsync(Guid id, CancellationToken ct)
    {
        var loaded = await LoadCurrent(id, null, ct);
        if (loaded.Error != null) return loaded.Error;
        if (!CheckoutSessionState.ShippingComplete(loaded.Draft!, loaded.Native!)) return new StorefrontFailure(422, "checkout_shipping_required");
        return new(await checkoutOptions.AvailablePaymentMethods(loaded.Native!));
    }

    public async Task<StorefrontResult<CheckoutSessionDto>> SetPaymentAsync(Guid id, CheckoutPaymentSelectionRequest input, Func<string> checkoutUrl, CancellationToken ct)
    {
        await using var handle = await drafts.Lock().AcquireAsync(TimeSpan.FromSeconds(10), ct);
        await request.Customer.GenericAttributes.ReloadAsync();
        var loaded = await LoadCurrent(id, input.Revision, ct);
        if (loaded.Error != null) return loaded.Error;
        var draft = loaded.Draft!;
        var native = loaded.Native!;
        if (!CheckoutSessionState.ShippingComplete(draft, native)) return new StorefrontFailure(422, "checkout_shipping_required");
        if (!native.Requirements.HasFlag(CheckoutRequirements.Payment)) return new StorefrontFailure(422, "payment_not_required");
        if (native.IsShippingRequired)
        {
            var offered = await checkoutOptions.QuoteShipping(native, draft.ShippingAddressId!.Value, ct);
            if (offered == null || !offered.Any(x => x.Id == draft.Shipping!.Id &&
                string.Equals(x.Provider, draft.Shipping.Provider, StringComparison.OrdinalIgnoreCase) &&
                x.Rate == draft.Shipping.Rate))
                return new StorefrontFailure(409, "shipping_rate_changed");
        }
        var available = await checkoutOptions.AvailablePaymentMethods(native);
        var method = available.FirstOrDefault(x => string.Equals(x.SystemName, input.SystemName, StringComparison.OrdinalIgnoreCase));
        if (method == null) return new StorefrontFailure(422, "payment_method_unavailable");
        var next = (draft with { Payment = method }).Advance();
        await drafts.SaveAsync(next, ct);
        return new(await ToDto(next, loaded.Cart!, native, checkoutUrl, ct));
    }

    // Explicitly copies a draft into native checkout data; separated from the fresh-request quote
    // because native calculation services cache address/payment dependent data within a request.

    public async Task<StorefrontResult<CheckoutReviewPreparedDto>> PrepareReviewAsync(Guid id, PrepareCheckoutReviewRequest input, CancellationToken ct)
    {
        if (!OfflineEnabled) return new StorefrontFailure(409, "native_checkout_disabled");
        await using var handle = await drafts.Lock().AcquireAsync(TimeSpan.FromSeconds(10), ct);
        await request.Customer.GenericAttributes.ReloadAsync();
        var draft = drafts.Read();
        if (draft == null || draft.Id != id) return new StorefrontFailure(404, "checkout_session_not_found");
        if (draft.Revision != input.Revision || draft.ExpiresAt <= DateTime.UtcNow)
            return new StorefrontFailure(409, "stale_checkout_revision");
        if (draft.PreparedCartFingerprint != null)
            return new StorefrontFailure(409, "checkout_review_already_prepared");
        var loaded = await LoadCurrent(id, input.Revision, ct);
        if (loaded.Error != null) return loaded.Error;
        var native = loaded.Native!;
        if (!CheckoutSessionState.ShippingComplete(draft, native) || draft.Payment == null)
            return new StorefrontFailure(422, "checkout_selection_required");
        if (!await offlineEligibility.SupportsAsync(native, draft)) return new StorefrontFailure(409, "checkout_requires_browser");
        var previous = await ReadAttempt(ct);
        if (previous != null && previous.Status == "started" && !await OrderExists(previous.SessionId, ct))
            return new StorefrontFailure(409, "checkout_attempt_requires_reconciliation");
        var billing = draft.BillingAddressId.HasValue ? await addressChecks.OwnedAddress(draft.BillingAddressId.Value, ct) : null;
        var delivery = draft.ShippingAddressId.HasValue ? await addressChecks.OwnedAddress(draft.ShippingAddressId.Value, ct) : null;
        ShippingOption? selected = null;
        if (native.IsShippingRequired)
        {
            var response = await shipping.GetShippingOptionsAsync(native, delivery, storeId: request.StoreId);
            var matches = response.ShippingOptions.Where(x => x.ShippingMethodId == draft.Shipping!.Id &&
                string.Equals(x.ShippingRateComputationMethodSystemName, draft.Shipping.Provider, StringComparison.OrdinalIgnoreCase) &&
                x.Rate == draft.Shipping.Rate).ToList();
            if (matches.Count != 1) return new StorefrontFailure(409, "shipping_rate_changed");
            selected = matches[0];
        }
        // Attach only previously validated customer-owned addresses through the tracked context.
        var customer = request.Customer;
        customer.BillingAddress = billing == null ? null : await db.Set<Address>().FirstAsync(x => x.Id == billing.Id, ct);
        customer.ShippingAddress = delivery == null ? null : await db.Set<Address>().FirstAsync(x => x.Id == delivery.Id, ct);
        var ga = customer.GenericAttributes;
        ga.SelectedShippingOption = selected;
        ga.OfferedShippingOptions = selected == null ? null : new List<ShippingOption> { selected };
        ga.SelectedPaymentMethod = draft.Payment.SystemName;
        ga.CheckoutOrderData = null;
        var next = (draft with { PreparedCartFingerprint = await sessionState.RawCartFingerprint(ct) }).Advance();
        await drafts.SaveAsync(next, ct);
        return new(new CheckoutReviewPreparedDto(next.Id, next.Revision));
    }

    public async Task<StorefrontResult<CheckoutReviewDto>> ReviewAsync(Guid id, CancellationToken ct)
    {
        if (!OfflineEnabled) return new StorefrontFailure(409, "native_checkout_disabled");
        var attempt = await ReadAttempt(ct);
        if (attempt?.SessionId == id) return new StorefrontFailure(409, "checkout_attempt_exists");
        if (attempt != null && attempt.Status == "started" && !await OrderExists(attempt.SessionId, ct))
            return new StorefrontFailure(409, "checkout_attempt_requires_reconciliation");
        var reviewed = await GetReview(id, null, ct);
        if (reviewed.Error != null) return reviewed.Error;
        return new(reviewed.Review);
    }

    // The order GUID is the draft GUID. Durable reservation is written before invoking Native placement.
    // No automated retry after any ambiguous result, even if the client disconnects.

    public async Task<StorefrontResult<CheckoutConfirmationDto>> ConfirmAsync(Guid id, ConfirmCheckoutRequest input,
        string key, CancellationToken ct)
    {
        if (!OfflineEnabled) return new StorefrontFailure(409, "native_checkout_disabled");
        if (!OfflineCheckoutPolicy.IsValidKey(key)) return new StorefrontFailure(400, "invalid_idempotency_key");
        if (!input.AcceptTerms || input.ExpectedOrderTotal is null || input.ExpectedOrderTotal < 0)
            return new StorefrontFailure(422, "checkout_confirmation_required");
        var parsedKey = Guid.Parse(key);
        await using var handle = await drafts.Lock().AcquireAsync(TimeSpan.FromSeconds(10), ct);

        var existing = await ReadAttempt(ct);
        if (existing?.SessionId == id)
        {
            if (!existing.Matches(parsedKey, input)) return new StorefrontFailure(409, "idempotency_conflict");
            return new(await ConfirmationState(id, existing, ct));
        }
        if (await StorefrontScope.Orders(db, request.Customer, request.StoreId).AnyAsync(x => x.OrderGuid == id, ct))
            return new StorefrontFailure(409, "order_already_exists");
        if (existing != null && existing.Status == "started" && !await OrderExists(existing.SessionId, ct))
            return new StorefrontFailure(409, "checkout_attempt_requires_reconciliation");

        var reviewed = await GetReview(id, input.Revision, ct);
        if (reviewed.Error != null) return reviewed.Error;
        var quote = reviewed.Review!;
        if (quote.PayableTotal != input.ExpectedOrderTotal || quote.CurrencyCode != input.CurrencyCode ||
            quote.CartFingerprint != input.CartFingerprint)
            return new StorefrontFailure(409, "order_total_or_cart_changed");
        if (!await processing.IsMinimumOrderPlacementIntervalValidAsync(request.Customer, stores.CurrentStore))
            return new StorefrontFailure(409, "minimum_order_interval");

        var attempt = new CheckoutAttempt(id, parsedKey, input.Revision, input.ExpectedOrderTotal.Value,
            input.CurrencyCode, input.CartFingerprint, DateTime.UtcNow);
        // Serializable reservation also protects across processes when the installed distributed lock is local.
        await using (var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            var row = await AttemptRows().SingleOrDefaultAsync(ct);
            var latest = row == null ? null : JsonSerializer.Deserialize<CheckoutAttempt>(row.Value);
            if (latest != null && (latest.SessionId == id ||
                latest.Status == "started" && !await OrderExists(latest.SessionId, ct)))
                return new StorefrontFailure(409, "checkout_attempt_exists");
            if (row == null)
            {
                row = new GenericAttribute { EntityId = request.Customer.Id, KeyGroup = request.Customer.GenericAttributes.EntityName,
                    StoreId = request.StoreId, Key = AttemptKey, Value = JsonSerializer.Serialize(attempt) };
                db.GenericAttributes.Add(row);
            }
            else row.Value = JsonSerializer.Serialize(attempt);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        // From here the server must finish or preserve the durable uncertain state. Client cancellation is not a retry signal.
        var payment = new ProcessPaymentRequest { CustomerId = request.Customer.Id, StoreId = request.StoreId,
            OrderGuid = id, PaymentMethodSystemName = reviewed.Draft!.Payment!.SystemName,
            ExpectedOrderTotal = input.ExpectedOrderTotal };
        try
        {
            var result = await processing.PlaceOrderAsync(payment, CancellationToken.None);
            if (!result.Success && result.PlacedOrder == null && !await OrderExists(id, CancellationToken.None))
            {
                var rejected = attempt with { Status = "rejected" };
                // Update only the attempt row, never flush tracked entities from a rolled-back placement.
                await AttemptRows().Where(x => x.Value == JsonSerializer.Serialize(attempt))
                    .ExecuteUpdateAsync(x => x.SetProperty(y => y.Value, JsonSerializer.Serialize(rejected)), CancellationToken.None);
                return new(await ConfirmationState(id, rejected, CancellationToken.None));
            }
        }
        catch (Exception)
        {
            // Preserve the reservation. A failure after native commit can still have produced an order.
            // Never automatically invoke PlaceOrder again. No payment/request data in the log.
            logger.LogWarning("Native checkout attempt {SessionId} requires outcome reconciliation.", id);
        }
        return new(await ConfirmationState(id, attempt, CancellationToken.None));
    }

    public async Task<StorefrontResult<CheckoutConfirmationDto>> ConfirmationAsync(Guid id, CancellationToken ct)
    {
        var attempt = await ReadAttempt(ct);
        if (attempt?.SessionId != id && !await StorefrontScope.Orders(db, request.Customer, request.StoreId)
            .AnyAsync(x => x.OrderGuid == id, ct)) return new StorefrontFailure(404, "checkout_attempt_not_found");
        return new(await ConfirmationState(id, attempt?.SessionId == id ? attempt : null, ct));
    }

    public async Task<StorefrontResult<bool>> AbandonAsync(Guid id, CancellationToken ct)
    {
        await using var handle = await drafts.Lock().AcquireAsync(TimeSpan.FromSeconds(10), ct);
        await request.Customer.GenericAttributes.ReloadAsync();
        var draft = drafts.Read();
        if (draft?.Id != id) return new StorefrontFailure(404, "checkout_session_not_found");
        // Does not remove a durable attempt or overwrite native selections.
        await drafts.ClearAsync(ct);
        return new(true);
    }

    private IQueryable<GenericAttribute> AttemptRows() => db.GenericAttributes.Where(x =>
        x.EntityId == request.Customer.Id && x.KeyGroup == request.Customer.GenericAttributes.EntityName &&
        x.StoreId == request.StoreId && x.Key == AttemptKey);

    private async Task<CheckoutAttempt?> ReadAttempt(CancellationToken ct)
    {
        var value = await AttemptRows().AsNoTracking().Select(x => x.Value).SingleOrDefaultAsync(ct);
        return value == null ? null : JsonSerializer.Deserialize<CheckoutAttempt>(value)
            ?? throw new InvalidOperationException("Invalid checkout attempt.");
    }

    private Task<bool> OrderExists(Guid id, CancellationToken ct) => db.Orders.IgnoreQueryFilters().AsNoTracking()
        .AnyAsync(x => x.OrderGuid == id, ct);

    private async Task<CheckoutConfirmationDto> ConfirmationState(Guid id, CheckoutAttempt? attempt, CancellationToken ct)
    {
        var order = await StorefrontScope.Orders(db, request.Customer, request.StoreId).AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderGuid == id, ct);
        if (order != null) return new(id, "placed", order.Id, order.OrderNumber, OrderStatusMapping.Payment(order.PaymentStatusId), false);
        var rejected = attempt?.Status == "rejected";
        return new(id, rejected ? "rejected" : "requiresReconciliation", null, null, null, !rejected);
    }

    private async Task<CheckoutReviewLoadResult> GetReview(Guid id, int? revision, CancellationToken ct)
    {
        var draft = drafts.Read();
        if (draft == null || draft.Id != id) return new(Error: new StorefrontFailure(404, "checkout_session_not_found"));
        if (draft.ExpiresAt <= DateTime.UtcNow || revision.HasValue && revision != draft.Revision)
            return new(Error: new StorefrontFailure(409, "stale_checkout_revision"));
        if (draft.PreparedCartFingerprint == null || !sessionState.NativeSelectionsMatch(draft) ||
            draft.PreparedCartFingerprint != await sessionState.RawCartFingerprint(ct) || !await addressChecks.AddressesCurrent(draft, ct))
            return new(Error: new StorefrontFailure(409, "checkout_review_stale"));
        var native = await NativeCart();
        if (!await offlineEligibility.SupportsAsync(native, draft)) return new(Error: new StorefrontFailure(409, "checkout_requires_browser"));
        if (native.IsShippingRequired)
        {
            var offered = await checkoutOptions.QuoteShipping(native, draft.ShippingAddressId!.Value, ct);
            if (offered?.Count(x => x.Id == draft.Shipping!.Id &&
                string.Equals(x.Provider, draft.Shipping.Provider, StringComparison.OrdinalIgnoreCase) &&
                x.Rate == draft.Shipping.Rate) != 1) return new(Error: new StorefrontFailure(409, "shipping_rate_changed"));
        }
        var warnings = new List<string>();
        var validating = new ValidatingCartEvent(native, warnings);
        await events.PublishAsync(validating);
        if (validating.Result != null || warnings.Count > 0) return new(Error: new StorefrontFailure(422, "checkout_native_validation_failed"));
        var payment = new ProcessPaymentRequest { CustomerId = request.Customer.Id, StoreId = request.StoreId,
            OrderGuid = id, PaymentMethodSystemName = draft.Payment!.SystemName };
        var (validationWarnings, _) = await processing.ValidateOrderPlacementAsync(payment, customer: request.Customer);
        if (validationWarnings.Count > 0)
        {
            return new(Error: new StorefrontFailure(422, "checkout_native_validation_failed", new Dictionary<string, object?>
            {
                ["warnings"] = validationWarnings
            }));
        }
        var total = await calculations.GetShoppingCartTotalAsync(native, new ShoppingCartTotalOptions { UseRequestCache = false });
        if (total.Total == null) return new(Error: new StorefrontFailure(422, "checkout_total_unavailable"));
        return new(draft, new CheckoutReviewDto(id, draft.Revision, currencies.PrimaryCurrency.CurrencyCode,
            total.Total.Value.Amount, draft.PreparedCartFingerprint, true));
    }

    private async Task<CheckoutDraftLoadResult> LoadCurrent(Guid id, int? expectedRevision, CancellationToken ct)
    {
        var draft = drafts.Read();
        if (draft == null || draft.Id != id) return new(Error: new StorefrontFailure(404, "checkout_session_not_found"));
        if (draft.ExpiresAt <= DateTime.UtcNow) return new(Error: new StorefrontFailure(409, "checkout_session_expired"));
        if (draft.PreparedCartFingerprint != null) return new(Error: new StorefrontFailure(409, "checkout_review_prepared_restart_to_edit"));
        if (expectedRevision.HasValue && expectedRevision != draft.Revision)
            return new(Error: new StorefrontFailure(RevisionConflict, "stale_checkout_revision"));
        var state = await cartFacade.StateAsync(ct);
        if (!draft.IsCurrent(state.Revision, DateTime.UtcNow)) return new(Error: new StorefrontFailure(409, "checkout_cart_changed"));
        if (!state.Checkout.CanStart) return new(Error: new StorefrontFailure(422, "checkout_not_ready"));
        if (!await addressChecks.AddressesCurrent(draft, ct)) return new(Error: new StorefrontFailure(409, "checkout_address_changed"));
        return new(draft, state, await NativeCart());
    }

    private Task<CheckoutSessionDto> ToDto(CheckoutDraft draft, CartStateDto state, ShoppingCart native, Func<string> checkoutUrl, CancellationToken ct) =>
        sessionState.ToDto(draft, state, native, ct, checkoutUrl);
}
