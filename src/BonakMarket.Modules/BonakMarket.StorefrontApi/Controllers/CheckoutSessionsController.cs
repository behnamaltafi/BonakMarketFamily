#nullable enable
using System.Text.Json;
using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BonakMarket.Core.Checkout.Attributes;
using BonakMarket.Core.Checkout.Cart.Events;
using BonakMarket.Core.Checkout.Orders.Handlers;
using BonakMarket.Core.Stores;
using BonakMarket.Events;
using Microsoft.AspNetCore.Mvc;
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
using BonakMarket.StorefrontApi.Services;
using BonakMarket.Threading;

namespace BonakMarket.StorefrontApi.Controllers;

/// <summary>Customer/store-scoped checkout preparation and opt-in native offline confirmation.</summary>
[Route("api/storefront/v1/checkout/sessions")]
[StorefrontPermission(StorefrontPermissions.Checkout)]
[StorefrontPermission(StorefrontPermissions.Cart)]
[StorefrontPermission(Permissions.Cart.AccessShoppingCart)]
[StorefrontPermission(Permissions.Catalog.DisplayPrice)]
public sealed class CheckoutSessionsController(SmartDbContext db, StorefrontRequestContext request,
    CartFacade cartFacade, IShoppingCartService carts, IShippingService shipping, IPaymentService payments,
    IAddressService addresses, IDistributedLockProvider locks, IConfiguration configuration,
    IOrderCalculationService calculations, IOrderProcessingService processing, IStoreContext stores,
    ICheckoutFactory checkoutFactory, ICheckoutAttributeMaterializer checkoutAttributes,
    ShoppingCartSettings cartSettings, ICurrencyService currencies, IEventPublisher events, ILogger<CheckoutSessionsController> logger) : StorefrontController
{
    private readonly CheckoutDraftStore _drafts = new(db, request, locks);
    private readonly CheckoutAddressChecks _addressChecks = new(db, request, addresses);
    private const int RevisionConflict = 409;
    private const string AttemptKey = "BonakMarket.Storefront.OrderAttempt.v1";
    private bool OfflineEnabled => configuration.GetValue<bool>("StorefrontApi:EnableOfflineCheckout") && db.DataProvider.ProviderType == DbSystemType.SqlServer;
    private Task<ShoppingCart> NativeCart() => carts.GetCartAsync(request.Customer, ShoppingCartType.ShoppingCart, request.StoreId);

    /// <summary>Creates or resumes the single current draft for this customer/store.</summary>
    [HttpPost]
    public async Task<ActionResult<CheckoutSessionDto>> Start(CancellationToken ct)
    {
        await using var handle = await _drafts.Lock().AcquireAsync(TimeSpan.FromSeconds(10), ct);
        await request.Customer.GenericAttributes.ReloadAsync();
        var state = await cartFacade.StateAsync(ct);
        if (!state.Checkout.CanStart)
        {
            var problem = new ProblemDetails { Status = 422, Title = "checkout_not_ready" };
            problem.Extensions["blockers"] = state.Checkout.Blockers;
            problem.Extensions["warnings"] = state.Warnings;
            return UnprocessableEntity(problem);
        }

        var native = await NativeCart();
        var draft = _drafts.Read();
        if (draft == null || draft.ExpiresAt <= DateTime.UtcNow || !await _addressChecks.AddressesCurrent(draft, ct) ||
            (draft.PreparedCartFingerprint == null ? !draft.IsCurrent(state.Revision, DateTime.UtcNow) :
                draft.PreparedCartFingerprint != await RawCartFingerprint(ct) || !NativeSelectionsMatch(draft)))
        {
            draft = CheckoutDraft.Create(state.Revision, DateTime.UtcNow);
            await _drafts.SaveAsync(draft, ct);
        }

        return Ok(await ToDto(draft, state, native, ct));
    }

    [HttpGet("current")]
    public async Task<ActionResult<CheckoutSessionDto>> Current(CancellationToken ct)
    {
        var draft = _drafts.Read();
        if (draft == null) return Error(404, "checkout_session_not_found");
        return Ok(await ToDto(draft, await cartFacade.StateAsync(ct), await NativeCart(), ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CheckoutSessionDto>> Get(Guid id, CancellationToken ct)
    {
        var draft = _drafts.Read();
        if (draft == null || draft.Id != id) return Error(404, "checkout_session_not_found");
        return Ok(await ToDto(draft, await cartFacade.StateAsync(ct), await NativeCart(), ct));
    }

    [HttpPut("{id:guid}/addresses")]
    public async Task<ActionResult<CheckoutSessionDto>> SetAddresses(Guid id, [FromBody] CheckoutAddressesRequest input, CancellationToken ct)
    {
        await using var handle = await _drafts.Lock().AcquireAsync(TimeSpan.FromSeconds(10), ct);
        await request.Customer.GenericAttributes.ReloadAsync();
        var loaded = await LoadCurrent(id, input.Revision, ct);
        if (loaded.Error != null) return loaded.Error;
        var draft = loaded.Draft!;
        var native = loaded.Native!;
        var needsBilling = native.Requirements.HasFlag(CheckoutRequirements.BillingAddress);
        if (needsBilling != input.BillingAddressId.HasValue || native.IsShippingRequired != input.ShippingAddressId.HasValue)
            return Error(422, "checkout_addresses_required");

        var billing = input.BillingAddressId.HasValue ? await _addressChecks.OwnedAddress(input.BillingAddressId.Value, ct) : null;
        var delivery = input.ShippingAddressId.HasValue ? await _addressChecks.OwnedAddress(input.ShippingAddressId.Value, ct) : null;
        if ((input.BillingAddressId.HasValue && billing == null) || (input.ShippingAddressId.HasValue && delivery == null))
            return Error(404, "address_not_found");
        if ((billing != null && !await _addressChecks.ValidAddress(billing, false)) || (delivery != null && !await _addressChecks.ValidAddress(delivery, true)))
            return Error(422, "invalid_checkout_address");

        var next = draft.WithAddresses(billing?.Id, delivery?.Id, CheckoutAddressChecks.AddressFingerprint(billing), CheckoutAddressChecks.AddressFingerprint(delivery)).Advance();
        await _drafts.SaveAsync(next, ct);
        return Ok(await ToDto(next, loaded.Cart!, native, ct));
    }

    [HttpGet("{id:guid}/shipping-options")]
    public async Task<ActionResult<IReadOnlyList<CheckoutShippingOptionDto>>> ShippingOptions(Guid id, CancellationToken ct)
    {
        var loaded = await LoadCurrent(id, null, ct);
        if (loaded.Error != null) return loaded.Error;
        var draft = loaded.Draft!;
        var native = loaded.Native!;
        if (!native.IsShippingRequired) return Ok(Array.Empty<CheckoutShippingOptionDto>());
        if (!AddressesComplete(draft, native)) return Error(422, "checkout_addresses_required");
        var options = await QuoteShipping(native, draft.ShippingAddressId!.Value, ct);
        if (options == null) return Error(422, "shipping_unavailable");
        return Ok(options);
    }

    [HttpPut("{id:guid}/shipping-method")]
    public async Task<ActionResult<CheckoutSessionDto>> SetShipping(Guid id, [FromBody] CheckoutShippingSelectionRequest input, CancellationToken ct)
    {
        await using var handle = await _drafts.Lock().AcquireAsync(TimeSpan.FromSeconds(10), ct);
        await request.Customer.GenericAttributes.ReloadAsync();
        var loaded = await LoadCurrent(id, input.Revision, ct);
        if (loaded.Error != null) return loaded.Error;
        var draft = loaded.Draft!;
        var native = loaded.Native!;
        if (!native.IsShippingRequired) return Error(422, "shipping_not_required");
        if (!AddressesComplete(draft, native)) return Error(422, "checkout_addresses_required");
        var options = await QuoteShipping(native, draft.ShippingAddressId!.Value, ct);
        var matches = options?.Where(x => x.Id == input.ShippingMethodId &&
            string.Equals(x.Provider, input.Provider, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches?.Count != 1) return Error(422, "shipping_option_unavailable");
        var option = matches[0];
        if (input.ObservedRate != option.Rate) return Error(409, "shipping_rate_changed");

        var next = draft.WithShipping(option).Advance();
        await _drafts.SaveAsync(next, ct);
        return Ok(await ToDto(next, loaded.Cart!, native, ct));
    }

    [HttpGet("{id:guid}/payment-methods")]
    public async Task<ActionResult<IReadOnlyList<CheckoutPaymentOptionDto>>> PaymentMethods(Guid id, CancellationToken ct)
    {
        var loaded = await LoadCurrent(id, null, ct);
        if (loaded.Error != null) return loaded.Error;
        if (!ShippingComplete(loaded.Draft!, loaded.Native!)) return Error(422, "checkout_shipping_required");
        return Ok(await AvailablePaymentMethods(loaded.Native!));
    }

    [HttpPut("{id:guid}/payment-method")]
    public async Task<ActionResult<CheckoutSessionDto>> SetPayment(Guid id, [FromBody] CheckoutPaymentSelectionRequest input, CancellationToken ct)
    {
        await using var handle = await _drafts.Lock().AcquireAsync(TimeSpan.FromSeconds(10), ct);
        await request.Customer.GenericAttributes.ReloadAsync();
        var loaded = await LoadCurrent(id, input.Revision, ct);
        if (loaded.Error != null) return loaded.Error;
        var draft = loaded.Draft!;
        var native = loaded.Native!;
        if (!ShippingComplete(draft, native)) return Error(422, "checkout_shipping_required");
        if (!native.Requirements.HasFlag(CheckoutRequirements.Payment)) return Error(422, "payment_not_required");
        if (native.IsShippingRequired)
        {
            var offered = await QuoteShipping(native, draft.ShippingAddressId!.Value, ct);
            if (offered == null || !offered.Any(x => x.Id == draft.Shipping!.Id &&
                string.Equals(x.Provider, draft.Shipping.Provider, StringComparison.OrdinalIgnoreCase) &&
                x.Rate == draft.Shipping.Rate))
                return Error(409, "shipping_rate_changed");
        }
        var available = await AvailablePaymentMethods(native);
        var method = available.FirstOrDefault(x => string.Equals(x.SystemName, input.SystemName, StringComparison.OrdinalIgnoreCase));
        if (method == null) return Error(422, "payment_method_unavailable");
        var next = (draft with { Payment = method }).Advance();
        await _drafts.SaveAsync(next, ct);
        return Ok(await ToDto(next, loaded.Cart!, native, ct));
    }

    // Explicitly copies a draft into native checkout data; separated from the fresh-request quote
    // because native calculation services cache address/payment dependent data within a request.
    [HttpPost("{id:guid}/review")]
    public async Task<ActionResult<CheckoutReviewPreparedDto>> PrepareReview(Guid id, [FromBody] PrepareCheckoutReviewRequest input, CancellationToken ct)
    {
        if (!OfflineEnabled) return Error(409, "native_checkout_disabled");
        await using var handle = await _drafts.Lock().AcquireAsync(TimeSpan.FromSeconds(10), ct);
        await request.Customer.GenericAttributes.ReloadAsync();
        var draft = _drafts.Read();
        if (draft == null || draft.Id != id) return Error(404, "checkout_session_not_found");
        if (draft.Revision != input.Revision || draft.ExpiresAt <= DateTime.UtcNow)
            return Error(409, "stale_checkout_revision");
        if (draft.PreparedCartFingerprint != null)
            return Error(409, "checkout_review_already_prepared");
        var loaded = await LoadCurrent(id, input.Revision, ct);
        if (loaded.Error != null) return loaded.Error;
        var native = loaded.Native!;
        if (!ShippingComplete(draft, native) || draft.Payment == null)
            return Error(422, "checkout_selection_required");
        if (!await SupportsOfflineCheckout(native, draft)) return Error(409, "checkout_requires_browser");
        var previous = await ReadAttempt(ct);
        if (previous != null && previous.Status == "started" && !await OrderExists(previous.SessionId, ct))
            return Error(409, "checkout_attempt_requires_reconciliation");
        var billing = draft.BillingAddressId.HasValue ? await _addressChecks.OwnedAddress(draft.BillingAddressId.Value, ct) : null;
        var delivery = draft.ShippingAddressId.HasValue ? await _addressChecks.OwnedAddress(draft.ShippingAddressId.Value, ct) : null;
        ShippingOption? selected = null;
        if (native.IsShippingRequired)
        {
            var response = await shipping.GetShippingOptionsAsync(native, delivery, storeId: request.StoreId);
            var matches = response.ShippingOptions.Where(x => x.ShippingMethodId == draft.Shipping!.Id &&
                string.Equals(x.ShippingRateComputationMethodSystemName, draft.Shipping.Provider, StringComparison.OrdinalIgnoreCase) &&
                x.Rate == draft.Shipping.Rate).ToList();
            if (matches.Count != 1) return Error(409, "shipping_rate_changed");
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
        var next = (draft with { PreparedCartFingerprint = await RawCartFingerprint(ct) }).Advance();
        await _drafts.SaveAsync(next, ct);
        return Ok(new CheckoutReviewPreparedDto(next.Id, next.Revision));
    }

    [HttpGet("{id:guid}/review")]
    public async Task<ActionResult<CheckoutReviewDto>> Review(Guid id, CancellationToken ct)
    {
        if (!OfflineEnabled) return Error(409, "native_checkout_disabled");
        var attempt = await ReadAttempt(ct);
        if (attempt?.SessionId == id) return Error(409, "checkout_attempt_exists");
        if (attempt != null && attempt.Status == "started" && !await OrderExists(attempt.SessionId, ct))
            return Error(409, "checkout_attempt_requires_reconciliation");
        var reviewed = await GetReview(id, null, ct);
        if (reviewed.Error != null) return reviewed.Error;
        return Ok(reviewed.Review);
    }

    // The order GUID is the draft GUID. Durable reservation is written before invoking Native placement.
    // No automated retry after any ambiguous result, even if the client disconnects.
    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<CheckoutConfirmationDto>> Confirm(Guid id, [FromBody] ConfirmCheckoutRequest input,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct)
    {
        if (!OfflineEnabled) return Error(409, "native_checkout_disabled");
        if (!OfflineCheckoutPolicy.IsValidKey(key)) return Error(400, "invalid_idempotency_key");
        if (!input.AcceptTerms || input.ExpectedOrderTotal is null || input.ExpectedOrderTotal < 0)
            return Error(422, "checkout_confirmation_required");
        var parsedKey = Guid.Parse(key);
        await using var handle = await _drafts.Lock().AcquireAsync(TimeSpan.FromSeconds(10), ct);

        var existing = await ReadAttempt(ct);
        if (existing?.SessionId == id)
        {
            if (!existing.Matches(parsedKey, input)) return Error(409, "idempotency_conflict");
            return Ok(await ConfirmationState(id, existing, ct));
        }
        if (await StorefrontScope.Orders(db, request.Customer, request.StoreId).AnyAsync(x => x.OrderGuid == id, ct))
            return Error(409, "order_already_exists");
        if (existing != null && existing.Status == "started" && !await OrderExists(existing.SessionId, ct))
            return Error(409, "checkout_attempt_requires_reconciliation");

        var reviewed = await GetReview(id, input.Revision, ct);
        if (reviewed.Error != null) return reviewed.Error;
        var quote = reviewed.Review!;
        if (quote.PayableTotal != input.ExpectedOrderTotal || quote.CurrencyCode != input.CurrencyCode ||
            quote.CartFingerprint != input.CartFingerprint)
            return Error(409, "order_total_or_cart_changed");
        if (!await processing.IsMinimumOrderPlacementIntervalValidAsync(request.Customer, stores.CurrentStore))
            return Error(409, "minimum_order_interval");

        var attempt = new CheckoutAttempt(id, parsedKey, input.Revision, input.ExpectedOrderTotal.Value,
            input.CurrencyCode, input.CartFingerprint, DateTime.UtcNow);
        // Serializable reservation also protects across processes when the installed distributed lock is local.
        await using (var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
        {
            var row = await AttemptRows().SingleOrDefaultAsync(ct);
            var latest = row == null ? null : JsonSerializer.Deserialize<CheckoutAttempt>(row.Value);
            if (latest != null && (latest.SessionId == id ||
                latest.Status == "started" && !await OrderExists(latest.SessionId, ct)))
                return Error(409, "checkout_attempt_exists");
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
                return Ok(await ConfirmationState(id, rejected, CancellationToken.None));
            }
        }
        catch (Exception)
        {
            // Preserve the reservation. A failure after native commit can still have produced an order.
            // Never automatically invoke PlaceOrder again. No payment/request data in the log.
            logger.LogWarning("Native checkout attempt {SessionId} requires outcome reconciliation.", id);
        }
        return Ok(await ConfirmationState(id, attempt, CancellationToken.None));
    }

    [HttpGet("{id:guid}/confirmation")]
    public async Task<ActionResult<CheckoutConfirmationDto>> Confirmation(Guid id, CancellationToken ct)
    {
        var attempt = await ReadAttempt(ct);
        if (attempt?.SessionId != id && !await StorefrontScope.Orders(db, request.Customer, request.StoreId)
            .AnyAsync(x => x.OrderGuid == id, ct)) return Error(404, "checkout_attempt_not_found");
        return Ok(await ConfirmationState(id, attempt?.SessionId == id ? attempt : null, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Abandon(Guid id, CancellationToken ct)
    {
        await using var handle = await _drafts.Lock().AcquireAsync(TimeSpan.FromSeconds(10), ct);
        await request.Customer.GenericAttributes.ReloadAsync();
        var draft = _drafts.Read();
        if (draft?.Id != id) return Error(404, "checkout_session_not_found");
        // Does not remove a durable attempt or overwrite native selections.
        await _drafts.ClearAsync(ct);
        return NoContent();
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

    private async Task<string> RawCartFingerprint(CancellationToken ct)
    {
        var rows = await StorefrontScope.Cart(db, request.Customer, request.StoreId).AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.ProductId, x.Quantity, x.Active, x.ParentItemId, x.BundleItemId,
                x.RawAttributes, x.CustomerEnteredPrice, x.UpdatedOnUtc }).ToListAsync(ct);
        var ga = request.Customer.GenericAttributes;
        return CartSnapshotRevision.Create(new { Rows = rows, ga.DiscountCouponCode, ga.RawGiftCardCouponCodes,
            ga.RawCheckoutAttributes, ga.UseRewardPointsDuringCheckout, ga.UseCreditBalanceDuringCheckout,
            request.Customer.GuildId, request.Customer.Email });
    }

    private bool NativeSelectionsMatch(CheckoutDraft draft)
    {
        var customer = request.Customer;
        var ga = customer.GenericAttributes;
        var option = ga.SelectedShippingOption;
        return customer.BillingAddressId == draft.BillingAddressId && customer.ShippingAddressId == draft.ShippingAddressId &&
            string.Equals(ga.SelectedPaymentMethod, draft.Payment?.SystemName, StringComparison.OrdinalIgnoreCase) &&
            (draft.Shipping == null ? option == null : option != null && option.ShippingMethodId == draft.Shipping.Id &&
                string.Equals(option.ShippingRateComputationMethodSystemName, draft.Shipping.Provider, StringComparison.OrdinalIgnoreCase) &&
                option.Rate == draft.Shipping.Rate);
    }

    private async Task<bool> SupportsOfflineCheckout(ShoppingCart native, CheckoutDraft draft)
    {
        var ga = request.Customer.GenericAttributes;
        if (!native.HasItems || native.ContainsRecurringItem() || native.Requirements != CheckoutRequirements.All ||
            cartSettings.CheckoutProcess != CheckoutProcess.Standard || cartSettings.ShowConfirmOrderLegalHint ||
            cartSettings.ThirdPartyEmailHandOver != CheckoutThirdPartyEmailHandOver.None ||
            ga.UseCreditBalanceDuringCheckout != 0 || ga.UseRewardPointsDuringCheckout || ga.GiftCardCouponCodes.Any() ||
            native.Items.Any(x => x.Item.Product == null || x.Item.Product.IsEsd || x.Item.Product.IsGiftCard))
            return false;
        var handlers = new[] { typeof(BillingAddressHandler), typeof(ShippingAddressHandler), typeof(ShippingMethodHandler),
            typeof(PaymentMethodHandler), typeof(ConfirmHandler) };
        if (!checkoutFactory.GetCheckoutSteps().Select(x => x.Handler.Metadata.HandlerType).SequenceEqual(handlers))
            return false;
        if ((await checkoutAttributes.GetCheckoutAttributesAsync(native, request.StoreId)).Count > 0) return false;
        if (draft.Payment == null) return false;
        var provider = await payments.LoadPaymentProviderBySystemNameAsync(draft.Payment.SystemName, true, request.StoreId);
        return provider != null && !provider.Value.RequiresInteraction && !provider.Value.RequiresConfirmation &&
            OfflineCheckoutPolicy.IsSupported(provider.Metadata.SystemName, provider.Value.GetType().FullName,
                provider.Metadata.ModuleDescriptor?.SystemName) &&
            await payments.IsPaymentProviderActiveAsync(provider.Metadata.SystemName, native, request.StoreId);
    }

    private sealed record ReviewResult(CheckoutDraft? Draft = null, CheckoutReviewDto? Review = null, ObjectResult? Error = null);

    private async Task<ReviewResult> GetReview(Guid id, int? revision, CancellationToken ct)
    {
        var draft = _drafts.Read();
        if (draft == null || draft.Id != id) return new(Error: Error(404, "checkout_session_not_found"));
        if (draft.ExpiresAt <= DateTime.UtcNow || revision.HasValue && revision != draft.Revision)
            return new(Error: Error(409, "stale_checkout_revision"));
        if (draft.PreparedCartFingerprint == null || !NativeSelectionsMatch(draft) ||
            draft.PreparedCartFingerprint != await RawCartFingerprint(ct) || !await _addressChecks.AddressesCurrent(draft, ct))
            return new(Error: Error(409, "checkout_review_stale"));
        var native = await NativeCart();
        if (!await SupportsOfflineCheckout(native, draft)) return new(Error: Error(409, "checkout_requires_browser"));
        if (native.IsShippingRequired)
        {
            var offered = await QuoteShipping(native, draft.ShippingAddressId!.Value, ct);
            if (offered?.Count(x => x.Id == draft.Shipping!.Id &&
                string.Equals(x.Provider, draft.Shipping.Provider, StringComparison.OrdinalIgnoreCase) &&
                x.Rate == draft.Shipping.Rate) != 1) return new(Error: Error(409, "shipping_rate_changed"));
        }
        var warnings = new List<string>();
        var validating = new ValidatingCartEvent(native, warnings);
        await events.PublishAsync(validating);
        if (validating.Result != null || warnings.Count > 0) return new(Error: Error(422, "checkout_native_validation_failed"));
        var payment = new ProcessPaymentRequest { CustomerId = request.Customer.Id, StoreId = request.StoreId,
            OrderGuid = id, PaymentMethodSystemName = draft.Payment!.SystemName };
        var (validationWarnings, _) = await processing.ValidateOrderPlacementAsync(payment, customer: request.Customer);
        if (validationWarnings.Count > 0)
        {
            var problem = new ProblemDetails { Status = 422, Title = "checkout_native_validation_failed" };
            problem.Extensions["warnings"] = validationWarnings;
            return new(Error: UnprocessableEntity(problem));
        }
        var total = await calculations.GetShoppingCartTotalAsync(native, new ShoppingCartTotalOptions { UseRequestCache = false });
        if (total.Total == null) return new(Error: Error(422, "checkout_total_unavailable"));
        return new(draft, new CheckoutReviewDto(id, draft.Revision, currencies.PrimaryCurrency.CurrencyCode,
            total.Total.Value.Amount, draft.PreparedCartFingerprint, true));
    }

    private sealed record LoadResult(CheckoutDraft? Draft = null, CartStateDto? Cart = null,
        ShoppingCart? Native = null, ObjectResult? Error = null);

    private async Task<LoadResult> LoadCurrent(Guid id, int? expectedRevision, CancellationToken ct)
    {
        var draft = _drafts.Read();
        if (draft == null || draft.Id != id) return new(Error: Error(404, "checkout_session_not_found"));
        if (draft.ExpiresAt <= DateTime.UtcNow) return new(Error: Error(409, "checkout_session_expired"));
        if (draft.PreparedCartFingerprint != null) return new(Error: Error(409, "checkout_review_prepared_restart_to_edit"));
        if (expectedRevision.HasValue && expectedRevision != draft.Revision)
            return new(Error: Error(RevisionConflict, "stale_checkout_revision"));
        var state = await cartFacade.StateAsync(ct);
        if (!draft.IsCurrent(state.Revision, DateTime.UtcNow)) return new(Error: Error(409, "checkout_cart_changed"));
        if (!state.Checkout.CanStart) return new(Error: Error(422, "checkout_not_ready"));
        if (!await _addressChecks.AddressesCurrent(draft, ct)) return new(Error: Error(409, "checkout_address_changed"));
        return new(draft, state, await NativeCart());
    }

    private static bool AddressesComplete(CheckoutDraft draft, ShoppingCart cart) =>
        (!cart.Requirements.HasFlag(CheckoutRequirements.BillingAddress) || draft.BillingAddressId.HasValue) &&
        (!cart.IsShippingRequired || draft.ShippingAddressId.HasValue);

    private static bool ShippingComplete(CheckoutDraft draft, ShoppingCart cart) =>
        AddressesComplete(draft, cart) && (!cart.IsShippingRequired || draft.Shipping != null);

    private async Task<List<CheckoutShippingOptionDto>?> QuoteShipping(ShoppingCart cart, int addressId, CancellationToken ct)
    {
        var address = await _addressChecks.OwnedAddress(addressId, ct);
        if (address == null || !await _addressChecks.ValidAddress(address, true)) return null;
        var quote = await shipping.CreateShippingOptionRequestAsync(cart, address, request.StoreId);
        var result = await shipping.GetShippingOptionsAsync(quote);
        if (result.ShippingOptions.Count == 0) return null;
        return result.ShippingOptions.Select(x => new CheckoutShippingOptionDto(x.ShippingMethodId,
            x.ShippingRateComputationMethodSystemName, x.Name, x.Description, x.Rate)).ToList();
    }

    private async Task<List<CheckoutPaymentOptionDto>> AvailablePaymentMethods(ShoppingCart cart)
    {
        if (!cart.Requirements.HasFlag(CheckoutRequirements.Payment)) return [];
        var methods = await payments.LoadActivePaymentProvidersAsync(cart, request.StoreId,
            PaymentMethodType.Standard | PaymentMethodType.Redirection);
        return methods.Where(x => x.Value.DisplayInPaymentMethodList &&
            (!cart.ContainsRecurringItem() || x.Value.RecurringPaymentType > RecurringPaymentType.NotSupported))
            .Select(x => new CheckoutPaymentOptionDto(x.Metadata.SystemName,
                x.Metadata.FriendlyName ?? x.Metadata.SystemName, x.Metadata.Description, x.Value.RequiresInteraction,
                x.Value.RequiresConfirmation, x.Value.RequiresPaymentSelection)).ToList();
    }

    private async Task<CheckoutSessionDto> ToDto(CheckoutDraft draft, CartStateDto state, ShoppingCart native, CancellationToken ct)
    {
        var blockers = state.Checkout.Blockers.ToList();
        var expired = draft.ExpiresAt <= DateTime.UtcNow;
        if (expired) blockers.Add("checkout_session_expired");
        if (draft.PreparedCartFingerprint == null
            ? !string.Equals(draft.CartRevision, state.Revision, StringComparison.Ordinal)
            : draft.PreparedCartFingerprint != await RawCartFingerprint(ct) || !NativeSelectionsMatch(draft))
            blockers.Add("checkout_cart_changed");
        if (!await _addressChecks.AddressesCurrent(draft, ct)) blockers.Add("checkout_address_changed");
        var status = expired ? "expired" : blockers.Count > 0 ? "stale" : "inProgress";
        var completed = new List<string>();
        var needsBilling = native.Requirements.HasFlag(CheckoutRequirements.BillingAddress);
        var needsPayment = native.Requirements.HasFlag(CheckoutRequirements.Payment);
        var addressesComplete = AddressesComplete(draft, native);
        if (addressesComplete) completed.Add("addresses");
        var shippingComplete = ShippingComplete(draft, native);
        if (shippingComplete && native.IsShippingRequired) completed.Add("shippingMethod");
        var paymentComplete = !needsPayment || draft.Payment != null;
        if (shippingComplete && needsPayment && paymentComplete) completed.Add("paymentMethod");
        var ready = blockers.Count == 0 && shippingComplete && paymentComplete;
        var step = status != "inProgress" ? "restart" : !addressesComplete ? "addresses" :
            !shippingComplete ? "shippingMethod" : !paymentComplete ? "paymentMethod" : "review";
        return new(draft.Id, draft.Revision, draft.CartRevision, ready ? (draft.PreparedCartFingerprint != null ? "reviewPrepared" : "readyForReview") : status, step,
            completed, draft.ExpiresAt, needsBilling, native.IsShippingRequired, needsPayment,
            draft.BillingAddressId, draft.ShippingAddressId, request.Customer.GenericAttributes.DefaultBillingAddressId,
            request.Customer.GenericAttributes.DefaultShippingAddressId, draft.Shipping, draft.Payment,
            state.Summary with { PayableTotal = null }, ready, false, blockers.Distinct().ToArray(),
            state.Warnings, true, Url.RouteUrl("Checkout") ?? Url.Content("~/checkout") ?? "/checkout");
    }
}
