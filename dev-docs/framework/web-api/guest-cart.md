# Guest shopping cart (Storefront API v1)

This module uses a separate native Guest Customer and the existing ShoppingCartItem table.
No HTTP session, MVC login cookie, identity changes, OData changes, or Redis are required.
GuestCartToken stores only a SHA-256 hash of a random 256-bit opaque token, a store/customer binding,
fixed expiry, and a merge receipt. Never log either credential. SQL Server is required.

## Client flow

1. Browse products anonymously; use canAddToCart, quantity rules and quote. Do not create a cart until first add.
2. POST /api/storefront/v1/cart/guest with neither Authorization nor X-Guest-Cart-Token. Response:
   {"guestCartToken":"<opaque>","expiresOnUtc":"<UTC ISO date>"}.
3. Securely persist the token scoped to the API origin and store. Mobile: Keychain/Keystore. Web: use a BFF
   with protected storage where available; an SPA may use local storage but is exposed to XSS. Do not send via URL.
4. Existing /cart, /cart/items (GET/POST/PUT/DELETE as applicable), /cart/total and /cart/coupon accept
   X-Guest-Cart-Token without Authorization. Public catalog also accepts the optional guest token to
   use the same pricing context. No token means local empty-cart UI, not a server cart; GET /cart without
   either credential is 401. Guest coupon application requires native price visibility. Handle guest-token 401 separately from Bearer refresh. Never create a new cart just because of a network error.
5. Each mutation returns CartStateDto. cartType is "guest" or "customer". Guest checkout blockers include
   authentication_required. Native display-price permissions remain authoritative. Do not infer hidden prices.
6. Complete existing OTP login. Keep the guest token until POST /api/storefront/v1/cart/merge succeeds.
   This request requires Authorization: Bearer <accessToken> AND X-Guest-Cart-Token: <guestToken>.
7. On 200, replace local state with returned registered cart, delete guest token. Retry merge safely after
   a network failure with both credentials. Replay returns current customer cart until original token expiry.
   If merge returns 409 guest_cart_merge_conflict, both carts are preserved; conflicts contain itemId, productId
   and code. To edit the guest cart while logged in, deliberately use the guest header only (omit Authorization);
   then retry merge with both. Do not retry other POST item mutations blindly; reload before deciding.
8. Other requests with valid JWT always use the registered cart, regardless of a guest header. Invalid JWT
   never falls back to guest. Guest tokens cannot be used for wishlist, profile, gift cards, rewards, orders or checkout.
   On logout, clear customer state/token; create a new guest cart lazily on the next add.
9. Merge revalidates native stock/quantity/configuration and customer ACL. It is all-or-nothing. Same product
   merging follows native settings, not a client-side sum. Inactive source items must be activated or removed
   before retry. Customer coupon wins; a guest coupon is reapplied only if no customer coupon exists. Generic
   warning codes guest_coupon_conflict or guest_coupon_not_applicable explain a discarded coupon.
   Prices are recalculated, no stock reservation or price guarantee. The existing API checkout draft is invalidated.

## Configuration and upgrade

- StorefrontApi:EnableGuestCart (default true); StorefrontApi:GuestCartExpirationDays (default 30, 1–90).
  Configuration is via appsettings/environment, no new admin settings page.
- Existing Identity:AllowedOrigins handles the custom request header. Native price/shop/cart permissions still apply.
- New storefront.guestcart permission defaults to Guests; verify it was installed on upgrades and native
  AccessShoppingCart is allowed for Guests. Module version incremented to refresh module permissions/resources.
- Apply module migration; verify StorefrontGuestCartToken table, indexes and cleanup task. Existing user data
  is not reinstalled. The cleanup task expires guest token records and scoped cart rows in bounded batches;
  native guest deletion then removes empty Guest Customers. Live empty carts are protected from native cleanup.
- Guest creation is rate-limited per IP per API instance plus native overload protection. Configure trustworthy
  forwarded headers and gateway-wide limits in multi-instance deployment.
- SQL Server application locks serialize participating Storefront cart/checkout/order mutations across replicas
  (same DB); merge also uses a serializable transaction and a persisted receipt. Native MVC does not participate
  in the new module-owned locks. Concurrent MVC stale read/write and native hook side effects are release risks,
  not claimed to be fully solved. Test against real SQL Server, including parallel API/MVC checkout and cart changes.

## Release gates (not executed by source editing)

Full .NET 10 build, focused GuestCartContractTests/StorefrontContractTests/SwaggerContractTests,
fresh installation + upgrade with existing customer records, native permissions & price ACL, CORS preflight,
token reuse after restart, cross-store/customer rejection, expiry/cleanup/empty token, concurrent add/merge
across two replicas, lost response after commit, failed copy after earlier line success, source orphan/inactive
line preservation, variants/required products, coupon conflict/reapply, checkout draft invalidation,
native MVC/OData/Identity regressions, and database rollback on deadlock/connection failure.
