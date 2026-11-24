# Cart and wishlist state

## Contract change on feature/storefront-api
GET /api/storefront/v1/cart now returns CartStateDto, not a bare array. GET /wishlist returns WishlistStateDto.
This is a deliberate breaking response change on the in-development feature branch; migrate any existing clients
before releasing. GET /cart/items and GET /wishlist/items expose the old basic read-only arrays.
POST/PUT/DELETE item operations return the updated respective state. Transfer returns both cart and wishlist,
because native settings determine whether the source is copied or removed. Coupon, gift card and rewards
mutations return 200 with cart state rather than 204. DELETE /cart/gift-cards clears stored gift coupons, including
ones no longer valid or individually discoverable. Existing paths, authentication and native ownership remain.

PUT /cart/items/{id}: {"quantity":2} or {"active":false}; active requires native AllowActivatableCartItems.
Wishlist only supports quantity updates. Native updates persist even when validation warns, so return 200
with current state and warnings rather than pretending no change occurred. Never automatically retry POST
or transfer after a network failure; reload the state first.

## Prices
Use the native calculation pipeline with actual cart selections, primary currency and native tax display mode.
Subtotal is before the cart-level subtotal discount, but already includes item-level prices/discounts.
Do not subtract line discounts again. Subtotal discount and order discount are separate. TaxAmount is the
currently known tax, already included if TaxInclusive is true. Shipping is nullable if not calculated.
EstimatedTotal is the current native calculated total, nullable; PayableTotal stays null until confirmed checkout.
Gift/reward/credit applied amounts are unknown while total is unknown. A valid card with no deduction has zero
applied amount once a total is known. Gift card codes are masked. Native valid gift-card list omits expired cards.
Only active roots contribute to cart totals; child lines are not counted twice. Inactive/bundle child rows need not
have independent prices. Wishlist has no payable total; prices are indicative only. If any active product is
unavailable/invalid, totals and pricing are withheld instead of silently omitting it.
Price visibility permission is checked; price fields and monetary warning details are not exposed otherwise.
Products no longer in the customer's store/catalog scope retain a removable owned row but no product metadata.

## Revision and checkout
Revision is a SHA-256 fingerprint of the observed representation and cart rows, including quantity, raw
selection and update timestamps. It can detect many native MVC/API changes after re-reading; it is not an
atomic precondition, lock, idempotency key or stock reservation. No If-Match enforcement is claimed.
Checkout.canStart is a preflight signal for current active lines, guild, permission and native cart validation;
checkout attributes, addresses, payment and final prices still require validation in checkout.
No Core, OData, web MVC, new tables or packages. Contract tests and Swagger tests added; execute integration
tests with SQL Server, real native price/tax/shipping and permissions before release.
