# Authenticated storefront coverage

Source audit: all public storefront controllers under src/BonakMarket.Web/Controllers; native Identity, CheckoutWorkflow/handlers, cart/order services, return, payment, downloads, product review and stock subscription paths. Scope is customer-facing commerce; admin and integration endpoints are deliberately separate. This is a source audit, not evidence of a successful runtime build.

## Native login gates and coverage

| Native controller / functionality | Login condition in native application | Storefront v1 coverage |
| --- | --- | --- |
| Customer Info, addresses; Identity ChangePassword; avatar | Registered customer | Profile and addresses already in Identity; password/avatars remain Identity scope, not added here |
| Cart and wishlist | Native role permissions; guests may be allowed | Private API: cart, wishlist, move/copy, coupon, gift card, reward points |
| Checkout start/address/shipping/payment/confirmation | Registered after guild changes; session-backed native workflow | Readiness, validation/start, shipping quotes, payment provider list; final selection/payment/order placement stays native browser |
| Customer Orders; Order Details/ReOrder | Registered history; order ownership/store in native detail | Existing orders/detail/reorder |
| Order Print / RePostPayment | Ownership/store; admin exceptions | Browser actions endpoint provides invoice URL and eligible payment details page, not direct PDF or payment execution |
| ShipmentDetails | Order ownership/store | Shipment list/detail; provider live tracking events not exposed |
| Customer CancelRecurringPayment | Registered + native cancellation policy | List and cancel recurring payments |
| Customer ReturnCases / ReturnCaseSubmit | Registered history; owned completed returnable order | List/detail/options/submit, server quantity cap and native policy |
| DownloadableProducts / GetDownload / GetLicense | Registered list; file access may be conditional in native settings | Strict owner/store checks for list, detail, agreement, file, license |
| RewardPoints | Registered + enabled feature | Balance/history and cart redemption preference |
| StockSubscriptions / BackInStockSubscribePopup | Native service requires registered customer | List, subscribe, unsubscribe; real email required by email-based native notification |
| Product ReviewsAdd / SetReviewHelpfulness | Conditional on anonymous-review setting | Private list/create/vote; form CAPTCHA and GDPR |
| EmailAFriend / EmailWishlistSend | Conditional on anonymous-sharing setting | Private share endpoints using native CAPTCHA, consent, registered email and persistent per-customer rate limit |
| Product/checkout attribute uploads | Not a strict login gate | Not added; file ownership and configuration upload require a separate contract |
| Product catalog, CMS, search, compare, recently viewed, newsletter, languages, currencies, contact, questions | No strict registered-user requirement | Existing public catalog API stays unchanged; public gaps outside this task |
| Admin, ERP/OData, installer, PDF template internals, external provider callbacks | Administrative/integration/provider boundary, not customer authentication | Not exposed as customer APIs |

## New routes
All relative to /api/storefront/v1; require HTTPS and a valid customer bearer token or native cookie. Cookie mutations require CSRF. Customer and store are never accepted as input.

- GET wishlist; POST wishlist/items; PUT/DELETE wishlist/items/{id}; POST wishlist/items/{id}/cart; POST cart/items/{id}/wishlist.
- POST cart/gift-cards; DELETE cart/gift-cards/{id}; PUT cart/reward-points.
- GET reward-points; GET stock-subscriptions; PUT stock-subscriptions/{productId}; DELETE stock-subscriptions/{id} (subscription id).
- GET orders/{orderId}/shipments; GET shipments/{id}; GET recurring-payments; POST recurring-payments/{id}/cancel.
- GET returns; GET returns/{id}; GET orders/{id}/return-options; POST orders/{id}/returns.
- GET downloads; GET downloads/{id}; POST downloads/{id}/file?agree=true&version=...; GET downloads/{id}/license. id is the purchased order-item GUID. File POST consumes a download allowance. A configured HTTPS external download may redirect; never forward bearer credentials to the external host.
- GET products/{productId}/reviews; POST products/{productId}/reviews (form-urlencoded or multipart, title/reviewText/rating/gdprConsent and native CAPTCHA fields); PUT products/{productId}/reviews/{reviewId}/helpfulness.
- POST products/{productId}/share; POST wishlist/share (form-urlencoded or multipart, friendEmail, personalMessage, gdprConsent, and native CAPTCHA fields). Five attempts per rolling hour per customer across both API routes; sender is the account email. Native MVC share forms retain their existing policy.
- GET checkout/readiness; POST checkout/start (validation/preflight, no payment or order creation); GET checkout/shipping-options?addressId=...; GET checkout/payment-methods.
- GET orders/{id}/browser-actions (invoice and native order-details URLs, native eligibility for retry).

## Browser checkout boundary

Native checkout handlers read HTTP route values, form fields, session CheckoutState and OrderPaymentInfo. Payment providers may require interactive widgets, redirects and callback recovery. Do not fake these by invoking a payment or creating an order from an arbitrary bearer request. The current checkout/start endpoint validates the existing cart and returns the native checkout URL. The client opens that URL in a browser with a valid native login cookie for the same customer, otherwise the browser must authenticate. A bearer token does not by itself log a browser in. Use the native invoice/repayment pages similarly. A complete headless checkout/payment session and idempotency contract is not implemented by this change.

## Deployment and tests

Confirm the Storefront permission provider has installed the new storefront.wishlist, storefront.rewardpoints, storefront.stocksubscriptions, storefront.checkout, storefront.returns, storefront.downloads, storefront.reviews, storefront.sharing permissions for the Registered role; guests only receive catalog access. Existing installations may need module permission synchronization. No new tables or migrations are introduced here. Validate against SQL Server and the installed payment/shipping providers before production. Run StorefrontContractTests and SwaggerContractTests, and add integration tests for cross-customer/store IDs, checkout handoff, concurrent returns/downloads, and payment provider behavior. No build/test execution is claimed by this source change.
