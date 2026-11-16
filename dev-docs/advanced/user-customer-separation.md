# User / Customer separation — integration progress

## Target business model

- `AppUser` is the authenticated person; login, roles, permissions, address book,
  cart, wishlist, loyalty/rewards, reviews and personal data belong to AppUser.
- `Customer` is the commercial buyer. `Order.CustomerId` remains the buyer.
- `AppUserCustomer` explicitly grants many-to-many user/buyer access.
- `Order.PlacedByAppUserId` records the human actor separately from the buyer.
- Shared catalog/store data is not user-owned.

## Implemented on this branch

- Independent AppUser and AppUserCustomer plus user-address and user-role mappings.
- New nullable AppUser ownership fields in multiple legacy domain records.
- `ICurrentAppUserContext` rejects invalid claims and never substitutes CustomerId.
- `LegacyIdentityCustomerId` is a **temporary explicit migration link** from
  the current login account to the AppUser identity. It is never inferred
  from phone number or email. When mapped, successful legacy sign-in supplies
  a trusted `app_user_guid` claim; unmatched legacy logins remain unchanged.
- User-scoped cart retrieval and mutation safeguards in the main shopping-cart service
  when a validated AppUser claim is available. A buyer switch does not migrate
  a signed-in AppUser's personal cart.
- Order placement checks explicit membership **before payment** for an AppUser
  session and stores `PlacedByAppUserId`; user-visible order routes check
  the buyer association.
- Permission checks switch to AppUser role mappings when the AppUser claim is present.
- `IAppUserBuyerContext` and the `api/app-user/customers` selection endpoint
  support an explicitly selected, authorized buyer independently of login.
  The selected buyer is reauthorized on each request; selection is cleared
  on logout. The buyer-selection cookie is **not** an authorization credential.
- Additional tests for invalid user claims and unauthorized buyer selection.

## IMPORTANT — not a complete runtime cutover

The code is deliberately a **compatibility bridge**. Legacy login still uses
Customer as its credential record; many existing account/profile/address,
reward, historical order-list, admin and checkout flows still query Customer
directly. The new nullable AppUser fields are not fully populated or used by
all old services. A signed-in AppUser can still encounter legacy flows that
interpret the selected buyer as the person.

**Do not merge or deploy as full identity separation.** Remaining work:
1. Migrate credentials to genuinely independent AppUser authentication and
   update all Login/Register/ForgotPassword/OTP/external identity paths,
   cookies, claims and account pages. Current bridge is transitional only.
2. Convert ALL user-oriented read/write flows (address book, roles, reviews,
   rewards/wallet, privacy, wishlists, admin/user management, recurring orders,
   historical records) from Customer to AppUser, and remove legacy user fields
   only when these paths no longer rely on them.
3. Finish customer-specific price/credit/address/checkout behavior with
   selected buyer and validate a full multi-buyer, multi-user test matrix.
4. Enforce order access at all public, admin and API entry points; review
   app-user authorization for background/recurring/system operations.
5. Run build, tests, integration/security/checkout regression tests and
   cross-provider database smoke tests after migrations exist.

## NO database migrations in this branch

As requested, there are **no migration scripts or installation migration changes**
in the PR. The repository owner will generate the final FluentMigrator changes
at the end. These must create the new AppUser/link tables, nullable user ownership
columns, temporary LegacyIdentityCustomerId, unique and foreign-key constraints,
and verified data backfills.

**Do not deploy this branch before creating/applying the schema**:
the updated sign-in bridge and new user-scoped services query AppUsers.

Tests were authored but NOT executed here: this environment has no .NET SDK.
SMS/OTP multi-provider redesign remains out of scope for this phase.
