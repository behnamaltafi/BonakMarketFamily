# Checkout session preparation (v1)

Base: /api/storefront/v1/checkout/sessions. Customer JWT or existing cookie+CSRF, HTTPS, native cart/checkout/display-price permissions.

- POST /sessions (relative to /checkout): creates or resumes the current draft, 200 with CheckoutSessionDto.
- GET /sessions/current or /sessions/{id}: resume on another device; expired/stale drafts are returned with blockers.
- PUT /sessions/{id}/addresses: {"revision":1,"billingAddressId":12,"shippingAddressId":12}
- GET /sessions/{id}/shipping-options: native provider options for selected owned shipping address.
- PUT /sessions/{id}/shipping-method: {"revision":2,"shippingMethodId":1,"provider":"...","observedRate":10000}
- GET /sessions/{id}/payment-methods: provisional native provider list.
- PUT /sessions/{id}/payment-method: {"revision":3,"systemName":"..."}.

Each successful mutation returns the updated draft and revision. A new address clears shipping/payment;
a new shipping selection clears payment. Always use returned revision. For 409 reload the draft or POST
a fresh draft if expired or cart/address changed. A changed shipping rate requires the user to select again.
Address selection must respect native requirements and owned Iranian billing/shipping address validation.
Shipping rate comes from the provider; it is not the final tax/discount-adjusted shipping charge.

## Storage and scope
One provisional draft per customer/store stored as JSON in the existing generic attribute table. 30-minute
fixed lifetime, no database migration. Existing distributed lock provider coordinates draft writes, and revision
is checked after acquiring it and reloading attributes. Lock scope depends on deployment provider; it does
not coordinate native MVC cart writes or guarantee atomicity across checkout and order placement.
The native cart revision detects observed state changes, not an atomic reservation. Addresses are revalidated
and fingerprinted. No native customer billing/shipping selection, payment data, browser session or order
is overwritten by draft endpoints. Legacy checkout routes remain available.

## Review and limitations
CartPreview is the native cart estimate before applying the draft selections. It is NOT a checkout total.
Selected shipping rate is a separate raw provider quote; do not add it to cartPreview on the client.
Payment eligibility can depend on actual address, shipping, payment data and provider-specific rules,
so the provider list is provisional. Free/zero-payable orders still require special handling at confirmation.
ReadyForReview means the draft selections are present; CanConfirm is deliberately false, PayableTotal
null and RequiresBrowserSession true. No confirm/payment endpoint is implemented in this phase; the existing
browser checkout URL starts native checkout and does not automatically import draft selections.
Final confirmation must revalidate cart, addresses, provider rate/payment eligibility, checkout attributes,
terms/waivers, final totals, and order idempotency using native services.
No Core/OData/MVC changes. Contract/Swagger test sources added; build and SQL-backed tests not run.
