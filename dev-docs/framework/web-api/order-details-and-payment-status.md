# Order details and payment status

All routes require the current authenticated customer/store scope.
GET /api/storefront/v1/orders retains its paged response and adds status objects, item counts,
stored customer currency/rate, paid date and up to four current product previews.
GET /orders/{id} retains order and items, adds stored financial amounts, order address snapshots,
shipment summaries, selected methods, comment and action capabilities. SKU and attribute description are
historical fields; currentProductName/currentImageUrl are explicitly live catalog metadata, nullable when
not visible. Render attributeDescriptionHtml in a sanitized renderer. No sensitive payment credentials,
authorization references or internal order notes are returned.

Amounts are stored native base-currency values, not converted to the customer currency. CustomerCurrencyCode
and CurrencyRate are historical conversion metadata, not the denomination of base amounts. A historic base
currency code is not persisted by the native Order entity; do not assume it equals today's shop currency.
Financials are a breakdown, not a client recomputation instruction. Status codes are stable; titles are
English fallback labels for client localization.

GET /orders/{id}/payment-status reads native order state. Authorized is not paid. Refund and void are
distinct. IsPaid means current native status is exactly Paid. PaidDateUtc records historical payment.
This endpoint does not contact or verify the provider and never changes state. Poll only while awaiting
a provider outcome; existing browser retry action remains native browser-only. Direct API invoice and
self-service cancellation are not advertised. BrowserInvoiceUrl requires native cookie login.

## Remaining native confirmation/payment integration
Checkout draft is not submitted by these changes. Core PlaceOrderAsync creates a DB transaction but calls
ProcessPayment before persisting the order. OrderGuid has a nonunique index. Native workflow consumes MVC
form/session payment information. A GUID alone is not a durable idempotency guarantee. Before adding confirm,
implement a durable per-attempt record/unique constraint, expected total verification immediately before
provider processing, and provider-specific idempotency/reconciliation for uncertain outcomes. Review each
supported provider's callback/redirect and form/confirmation requirements. Do not expose an unrestricted
payment retry or client-supplied paid status. Native PDF helper is in BonakMarket.Web, not a module service.
No Core, OData, Web, package or migration changes in this commit. Contract/Swagger tests added but not run.
