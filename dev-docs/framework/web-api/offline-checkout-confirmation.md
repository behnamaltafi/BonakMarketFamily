# Optional native offline checkout confirmation — disabled by default

This implementation is a controlled staging feature, not a production readiness claim.
Enable only after SQL Server integration tests and operator review using StorefrontApi:EnableOfflineCheckout=true.
Never enable for mixed native MVC/API concurrent checkout until coordination across both entry points is tested.
No external provider, recurring order, gift card/reward/credit redemption, checkout attribute form,
custom handler, legal-hint or third-party consent flow is supported by this initial native confirmation path.
Those use native browser checkout. Only exact audited built-in CashOnDelivery/Prepayment provider implementations,
enabled by the merchant and active for the cart, are accepted. The provider's configured native transaction status
is not a bank confirmation. No payment gateway attempt API is exposed.

1. Complete checkout draft selections.
2. POST /api/storefront/v1/checkout/sessions/{id}/review with {"revision":4}.
   This explicit step copies verified address/shipping/payment selections into native customer checkout state,
   unlike earlier draft-only operations. It can replace a browser's previous checkout selections.
3. GET /sessions/{id}/review in a fresh request, which validates the native cart and returns the primary-currency
   payableTotal, revision and fingerprint. Show the legal/order details and accepted amount to the user.
4. POST /sessions/{id}/confirm with Idempotency-Key (nonempty UUID) and
   {"revision":5,"expectedOrderTotal":123,"currencyCode":"IRR","cartFingerprint":"...","acceptTerms":true}.
   Use identical key and payload on retry. Do not retry with a different key. The server invokes Native PlaceOrderAsync.
5. GET /sessions/{id}/confirmation to recover the outcome; GET /orders/{id}/payment-status for native payment state.
   placed means an order exists, not necessarily paid. rejected requires a new draft. requiresReconciliation means
   the outcome is uncertain; do not retry or create a replacement order until an operator has reconciled it.
   A rejected result can be read again without invoking native placement.
6. DELETE /sessions/{id} abandons draft only, not a payment attempt or native selections.

One latest attempt per customer/store is persisted in existing GenericAttribute, under a serializable SQL Server
reservation transaction before placement. The native order GUID equals the draft ID. A duplicate GUID is prevented
by the approved unique index; a previously placed order can be looked up through confirmation even after a newer
attempt. A pending unresolved attempt blocks new API confirmations across drafts. Reconciliation of an attempt with
no persisted order requires operator investigation; do not clear the record automatically. The draft lock does not
serialize unrelated browser/cart writes. Fingerprints and the accepted-total Core guard reduce stale data risk but
do not guarantee atomic coordination with all cart mutations. Keep the feature off until verified. No new Core or OData edits.
