#nullable enable
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Checkout.Payment;
using BonakMarket.Core.Checkout.Shipping;
using BonakMarket.Core.Common;
using BonakMarket.Core.Common.Services;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Security;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.StorefrontApi.Services;
using BonakMarket.Threading;

namespace BonakMarket.StorefrontApi.Controllers;

/// <summary>Customer/store-scoped checkout preparation. Order creation is deliberately not executed here.</summary>
[Route("api/storefront/v1/checkout/sessions")]
[StorefrontPermission(StorefrontPermissions.Checkout)]
[StorefrontPermission(StorefrontPermissions.Cart)]
[StorefrontPermission(Permissions.Cart.AccessShoppingCart)]
[StorefrontPermission(Permissions.Catalog.DisplayPrice)]
public sealed class CheckoutSessionsController(SmartDbContext db, StorefrontRequestContext request,
    CartFacade cartFacade, IShoppingCartService carts, IShippingService shipping, IPaymentService payments,
    IAddressService addresses, IDistributedLockProvider locks) : StorefrontController
{
    private const string DraftKey = "BonakMarket.Storefront.CheckoutDraft.v1";
    private const int RevisionConflict = 409;
    private Task<ShoppingCart> NativeCart() => carts.GetCartAsync(request.Customer, ShoppingCartType.ShoppingCart, request.StoreId);

    private CheckoutDraft? ReadDraft()
    {
        var value = request.Customer.GenericAttributes.Get<string>(DraftKey, request.StoreId);
        if (string.IsNullOrEmpty(value)) return null;
        try { return JsonSerializer.Deserialize<CheckoutDraft>(value); }
        catch (JsonException) { return null; }
    }

    private async Task SaveDraft(CheckoutDraft draft, CancellationToken ct)
    {
        request.Customer.GenericAttributes.Set(DraftKey, JsonSerializer.Serialize(draft), request.StoreId);
        await db.SaveChangesAsync(ct);
    }

    private IDistributedLock DraftLock() => locks.GetLock($"storefront.checkout-draft:{request.StoreId}:{request.Customer.Id}");

    /// <summary>Creates or resumes the single current draft for this customer/store.</summary>
    [HttpPost]
    public async Task<ActionResult<CheckoutSessionDto>> Start(CancellationToken ct)
    {
        await using var handle = await DraftLock().AcquireAsync(TimeSpan.FromSeconds(10), ct);
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
        var draft = ReadDraft();
        if (draft == null || !draft.IsCurrent(state.Revision, DateTime.UtcNow) || !await AddressesCurrent(draft, ct))
        {
            draft = CheckoutDraft.Create(state.Revision, DateTime.UtcNow);
            await SaveDraft(draft, ct);
        }

        return Ok(await ToDto(draft, state, native, ct));
    }

    [HttpGet("current")]
    public async Task<ActionResult<CheckoutSessionDto>> Current(CancellationToken ct)
    {
        var draft = ReadDraft();
        if (draft == null) return Error(404, "checkout_session_not_found");
        return Ok(await ToDto(draft, await cartFacade.StateAsync(ct), await NativeCart(), ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CheckoutSessionDto>> Get(Guid id, CancellationToken ct)
    {
        var draft = ReadDraft();
        if (draft == null || draft.Id != id) return Error(404, "checkout_session_not_found");
        return Ok(await ToDto(draft, await cartFacade.StateAsync(ct), await NativeCart(), ct));
    }

    [HttpPut("{id:guid}/addresses")]
    public async Task<ActionResult<CheckoutSessionDto>> SetAddresses(Guid id, [FromBody] CheckoutAddressesRequest input, CancellationToken ct)
    {
        await using var handle = await DraftLock().AcquireAsync(TimeSpan.FromSeconds(10), ct);
        await request.Customer.GenericAttributes.ReloadAsync();
        var loaded = await LoadCurrent(id, input.Revision, ct);
        if (loaded.Error != null) return loaded.Error;
        var draft = loaded.Draft!;
        var native = loaded.Native!;
        var needsBilling = native.Requirements.HasFlag(CheckoutRequirements.BillingAddress);
        if (needsBilling != input.BillingAddressId.HasValue || native.IsShippingRequired != input.ShippingAddressId.HasValue)
            return Error(422, "checkout_addresses_required");

        var billing = input.BillingAddressId.HasValue ? await OwnedAddress(input.BillingAddressId.Value, ct) : null;
        var delivery = input.ShippingAddressId.HasValue ? await OwnedAddress(input.ShippingAddressId.Value, ct) : null;
        if ((input.BillingAddressId.HasValue && billing == null) || (input.ShippingAddressId.HasValue && delivery == null))
            return Error(404, "address_not_found");
        if ((billing != null && !await ValidAddress(billing, false)) || (delivery != null && !await ValidAddress(delivery, true)))
            return Error(422, "invalid_checkout_address");

        var next = draft.WithAddresses(billing?.Id, delivery?.Id, AddressFingerprint(billing), AddressFingerprint(delivery)).Advance();
        await SaveDraft(next, ct);
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
        await using var handle = await DraftLock().AcquireAsync(TimeSpan.FromSeconds(10), ct);
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
        await SaveDraft(next, ct);
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
        await using var handle = await DraftLock().AcquireAsync(TimeSpan.FromSeconds(10), ct);
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
        await SaveDraft(next, ct);
        return Ok(await ToDto(next, loaded.Cart!, native, ct));
    }

    private sealed record LoadResult(CheckoutDraft? Draft = null, CartStateDto? Cart = null,
        ShoppingCart? Native = null, ObjectResult? Error = null);

    private async Task<LoadResult> LoadCurrent(Guid id, int? expectedRevision, CancellationToken ct)
    {
        var draft = ReadDraft();
        if (draft == null || draft.Id != id) return new(Error: Error(404, "checkout_session_not_found"));
        if (draft.ExpiresAt <= DateTime.UtcNow) return new(Error: Error(409, "checkout_session_expired"));
        if (expectedRevision.HasValue && expectedRevision != draft.Revision)
            return new(Error: Error(RevisionConflict, "stale_checkout_revision"));
        var state = await cartFacade.StateAsync(ct);
        if (!draft.IsCurrent(state.Revision, DateTime.UtcNow)) return new(Error: Error(409, "checkout_cart_changed"));
        if (!state.Checkout.CanStart) return new(Error: Error(422, "checkout_not_ready"));
        if (!await AddressesCurrent(draft, ct)) return new(Error: Error(409, "checkout_address_changed"));
        return new(draft, state, await NativeCart());
    }

    private Task<Address?> OwnedAddress(int id, CancellationToken ct) =>
        db.Customers.AsNoTracking().Where(x => x.Id == request.Customer.Id).SelectMany(x => x.Addresses)
            .Include(x => x.Country).Include(x => x.StateProvince).FirstOrDefaultAsync(x => x.Id == id, ct);

    private async Task<bool> ValidAddress(Address address, bool forShipping) =>
        address.Country?.TwoLetterIsoCode == "IR" && address.Country.Published &&
        (forShipping ? address.Country.AllowsShipping : address.Country.AllowsBilling) &&
        await addresses.IsAddressValidAsync(address);

    private static string? AddressFingerprint(Address? address) => address == null ? null :
        CartSnapshotRevision.Create(new { address.Id, address.CountryId, address.StateProvinceId, address.City,
            address.Address1, address.Address2, address.ZipPostalCode, address.PhoneNumber, address.FirstName,
            address.LastName, address.Email, address.Company, address.FaxNumber, address.Salutation, address.Title });

    private async Task<bool> AddressesCurrent(CheckoutDraft draft, CancellationToken ct)
    {
        if (draft.BillingAddressId.HasValue)
        {
            var address = await OwnedAddress(draft.BillingAddressId.Value, ct);
            if (address == null || AddressFingerprint(address) != draft.BillingFingerprint || !await ValidAddress(address, false))
                return false;
        }
        if (draft.ShippingAddressId.HasValue)
        {
            var address = await OwnedAddress(draft.ShippingAddressId.Value, ct);
            if (address == null || AddressFingerprint(address) != draft.ShippingFingerprint || !await ValidAddress(address, true))
                return false;
        }
        return true;
    }

    private static bool AddressesComplete(CheckoutDraft draft, ShoppingCart cart) =>
        (!cart.Requirements.HasFlag(CheckoutRequirements.BillingAddress) || draft.BillingAddressId.HasValue) &&
        (!cart.IsShippingRequired || draft.ShippingAddressId.HasValue);

    private static bool ShippingComplete(CheckoutDraft draft, ShoppingCart cart) =>
        AddressesComplete(draft, cart) && (!cart.IsShippingRequired || draft.Shipping != null);

    private async Task<List<CheckoutShippingOptionDto>?> QuoteShipping(ShoppingCart cart, int addressId, CancellationToken ct)
    {
        var address = await OwnedAddress(addressId, ct);
        if (address == null || !await ValidAddress(address, true)) return null;
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
        if (!string.Equals(draft.CartRevision, state.Revision, StringComparison.Ordinal)) blockers.Add("checkout_cart_changed");
        if (!await AddressesCurrent(draft, ct)) blockers.Add("checkout_address_changed");
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
        return new(draft.Id, draft.Revision, draft.CartRevision, ready ? "readyForReview" : status, step,
            completed, draft.ExpiresAt, needsBilling, native.IsShippingRequired, needsPayment,
            draft.BillingAddressId, draft.ShippingAddressId, request.Customer.GenericAttributes.DefaultBillingAddressId,
            request.Customer.GenericAttributes.DefaultShippingAddressId, draft.Shipping, draft.Payment,
            state.Summary with { PayableTotal = null }, ready, false, blockers.Distinct().ToArray(),
            state.Warnings, true, Url.RouteUrl("Checkout") ?? Url.Content("~/checkout") ?? "/checkout");
    }
}
