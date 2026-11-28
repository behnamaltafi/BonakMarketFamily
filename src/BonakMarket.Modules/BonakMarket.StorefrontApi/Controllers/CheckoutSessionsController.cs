#nullable enable
using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Security;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Controllers;

/// <summary>Customer/store-scoped checkout preparation and opt-in native offline confirmation.</summary>
[Route("api/storefront/v1/checkout/sessions")]
[StorefrontPermission(StorefrontPermissions.Checkout)]
[StorefrontPermission(StorefrontPermissions.Cart)]
[StorefrontPermission(Permissions.Cart.AccessShoppingCart)]
[StorefrontPermission(Permissions.Catalog.DisplayPrice)]
public sealed class CheckoutSessionsController(CheckoutSessionService checkout) : StorefrontController
{
    private string CheckoutUrl() => Url.RouteUrl("Checkout") ?? Url.Content("~/checkout") ?? "/checkout";

    /// <summary>Creates or resumes the single current draft for this customer/store.</summary>
    [HttpPost]
    public async Task<ActionResult<CheckoutSessionDto>> Start(CancellationToken ct) =>
        Respond(await checkout.StartAsync(CheckoutUrl, ct));

    [HttpGet("current")]
    public async Task<ActionResult<CheckoutSessionDto>> Current(CancellationToken ct) =>
        Respond(await checkout.CurrentAsync(CheckoutUrl, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CheckoutSessionDto>> Get(Guid id, CancellationToken ct) =>
        Respond(await checkout.GetAsync(id, CheckoutUrl, ct));

    [HttpPut("{id:guid}/addresses")]
    public async Task<ActionResult<CheckoutSessionDto>> SetAddresses(Guid id, [FromBody] CheckoutAddressesRequest input, CancellationToken ct) =>
        Respond(await checkout.SetAddressesAsync(id, input, CheckoutUrl, ct));

    [HttpGet("{id:guid}/shipping-options")]
    public async Task<ActionResult<IReadOnlyList<CheckoutShippingOptionDto>>> ShippingOptions(Guid id, CancellationToken ct) =>
        Respond(await checkout.ShippingOptionsAsync(id, ct));

    [HttpPut("{id:guid}/shipping-method")]
    public async Task<ActionResult<CheckoutSessionDto>> SetShipping(Guid id, [FromBody] CheckoutShippingSelectionRequest input, CancellationToken ct) =>
        Respond(await checkout.SetShippingAsync(id, input, CheckoutUrl, ct));

    [HttpGet("{id:guid}/payment-methods")]
    public async Task<ActionResult<IReadOnlyList<CheckoutPaymentOptionDto>>> PaymentMethods(Guid id, CancellationToken ct) =>
        Respond(await checkout.PaymentMethodsAsync(id, ct));

    [HttpPut("{id:guid}/payment-method")]
    public async Task<ActionResult<CheckoutSessionDto>> SetPayment(Guid id, [FromBody] CheckoutPaymentSelectionRequest input, CancellationToken ct) =>
        Respond(await checkout.SetPaymentAsync(id, input, CheckoutUrl, ct));

    // Explicitly copies a draft into native checkout data; separated from the fresh-request quote
    // because native calculation services cache address/payment dependent data within a request.
    [HttpPost("{id:guid}/review")]
    public async Task<ActionResult<CheckoutReviewPreparedDto>> PrepareReview(Guid id, [FromBody] PrepareCheckoutReviewRequest input, CancellationToken ct) =>
        Respond(await checkout.PrepareReviewAsync(id, input, ct));

    [HttpGet("{id:guid}/review")]
    public async Task<ActionResult<CheckoutReviewDto>> Review(Guid id, CancellationToken ct) =>
        Respond(await checkout.ReviewAsync(id, ct));

    // The order GUID is the draft GUID. Durable reservation is written before invoking Native placement.
    // No automated retry after any ambiguous result, even if the client disconnects.
    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<CheckoutConfirmationDto>> Confirm(Guid id, [FromBody] ConfirmCheckoutRequest input,
        [FromHeader(Name = "Idempotency-Key")] string key, CancellationToken ct) =>
        Respond(await checkout.ConfirmAsync(id, input, key, ct));

    [HttpGet("{id:guid}/confirmation")]
    public async Task<ActionResult<CheckoutConfirmationDto>> Confirmation(Guid id, CancellationToken ct) =>
        Respond(await checkout.ConfirmationAsync(id, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Abandon(Guid id, CancellationToken ct) =>
        RespondNoContent(await checkout.AbandonAsync(id, ct));
}
