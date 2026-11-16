# User–Customer separation: phase-1 integration and remaining work

> **DRAFT / DO NOT DEPLOY OR MERGE AS A COMPLETED CUTOVER.**
> No database migrations are included. The repository owner will author them
> at the end, once the domain and runtime work are complete.

## Target ownership and identity boundary

- **AppUser** is an authenticated person. Person-specific logins, roles,
  permissions, address books, carts, wishlists, reviews, reward balances,
  preferences and other personal records belong to an AppUser.
- **Customer** is a business buyer/account. `Order.CustomerId` identifies
  the buyer on an order, not the user who placed it.
- **AppUserCustomer** is an explicit many-to-many permission link:
  multiple people may represent one Customer, and a person may represent
  multiple Customers.
- **Order.PlacedByAppUserId** identifies the human who placed the order.
- Catalog/store/shared resources are not automatically user-owned.

## Changes currently on this branch

1. New AppUser, AppUserCustomer, AppUserAddressMapping and
   AppUserRoleMapping models and corresponding infrastructure.
2. Nullable transitional AppUserId references on several existing personal
   entities. Legacy CustomerId references have not all been removed.
3. CurrentAppUserContext validates a dedicated `app_user_guid` claim and
   rejects invalid/missing identities; it never interprets Customer.Id as User.Id.
4. Explicit `LegacyIdentityCustomerId` supports a *reviewed* migration
   mapping between the **old login account** and AppUser. Legacy sign-in
   issues a dedicated claim only for an explicitly linked, active AppUser.
   No user/buyer link is created automatically from email or mobile number.
5. Permission evaluation in AppUser sessions uses user role mappings,
   not the roles of the selected commercial buyer.
6. The personal cart uses `AppUserId` for AppUser session reads/writes.
   Additional guards reject cross-user item mutations and whole-cart deletion.
   Buyer switching never migrates a personal cart.
7. Buyer selection lives in `IAppUserBuyerContext` and the
   `api/app-user/customers` endpoints; membership is revalidated when used,
   including after revocation. Logout clears the selection cookie.
8. **Security correction:** the existing WorkContext remains tied to the
   legacy login/session. It is *never* globally replaced with the selected
   commercial buyer, because legacy account/profile controllers edit it.
9. At the order boundary, placement requires an explicitly selected,
   authorized business buyer, separately checks that the old checkout
   CustomerId is exactly `AppUser.LegacyIdentityCustomerId`, and persists
   the selected buyer in `Order.CustomerId`; human actor is separately
   recorded in `Order.PlacedByAppUserId`.
10. User-facing order operations check buyer membership; legacy order-history
    pages are restricted to the selected buyer when an AppUser session exists.
11. New read-only endpoints `api/app-user/orders` and
    `api/app-user/profile` expose only orders of currently authorized buyers,
    and only the authenticated user's own profile/address/role/cart records.
12. Added NUnit unit tests for legacy/invalid identity claims, buyer-cookie
    validation, and membership checks. **Tests have not been executed.**

## Important unresolved cutover work

- The existing **primary** username/password/registration/reset/OTP and
  external-authentication flows still use Customer-based credentials.
  In particular, Smartstore still registers `AddIdentity<Customer, CustomerRole>()`.
  The reviewed mapping is a transitional bridge, not independent AppUser login.
- Many older storefront/admin/controllers and services still read/write
  Customer-owned address books, permissions, accounts, rewards/wallet,
  privacy/reviews, recurring orders and other personal data.
  New AppUser columns are not an end-to-end data migration.
- Legacy checkout calculations, discounts, taxation, shipping, credit/rewards
  and payment gateways may still assume the old login CustomerId equals the
  commercial buyer. Before a production cutover, **audit every pricing/payment
  dependency** and implement the intended user/buyer separation consistently.
- API/admin/background/recurring-order authorization paths need the same
  explicit buyer boundary and regression coverage.
- Existing customer records, cart data, roles, addresses, order and identity
  relationships need a reviewed backfill plan. Do not infer access merely
  because two entities have the same phone/email.
- Run `dotnet build`, targeted unit tests, full authentication/authorization
  tests, security and checkout/payment regressions and database smoke tests.

## Migration policy — explicitly deferred

Do **not** add FluentMigrator migrations or change installation schema in this
branch. At the end, the repository owner will create the migration(s) to
safely establish user/link tables, indexes, unique constraints and foreign
keys; populate verified legacy mappings and per-record user ownership; verify
data integrity; and only then retire legacy relationships.

**This branch cannot be run against an unchanged database:** new AppUser
tables and AppUserId columns will not exist. Creating/applying the schema and
staged backfill is a prerequisite for functional integration tests and deployment.

## Minimum test scenarios after migrations

1. A single AppUser with membership in two buyers can switch buyers
   while the personal cart and roles remain unchanged.
2. Two AppUsers representing the same buyer can access that buyer's orders,
   but not each other's personal addresses/cart/roles.
3. A forged or stale buyer-selection cookie, revoked membership and an inactive
   user/buyer must fail closed.
4. A caller must not specify an unrelated legacy checkout CustomerId.
5. A user with matching email/mobile but **no membership** cannot view,
   modify, pay for or create an order for the buyer.
6. Admin/system/background operations must not accidentally inherit a
   shopper's buyer authorization or accept it by default.
7. Orders must store the business CustomerId and actor AppUserId correctly,
   and pricing/payment/tax/credit/returns remain consistent.
8. Register/login/logout/OTP/password-reset/external auth and full account
   CRUD must be verified after moving them to AppUser.


## Latest integration: person-owned address book

- New `IAppUserAddressBookService` and a dedicated authenticated
  `api/app-user/addresses` CRUD API in both stores.
- The legacy storefront address list/add/edit/delete UI is routed through
  AppUser personal address mappings when a validated AppUser claim is present,
  rather than through the selected commercial buyer.
- Copy-on-write address editing allocates a new Address and updates only the
  current AppUser link. Deleting an entry removes only the mapping; it never
  deletes the address row referenced by old orders or another person.
- `AppUser.DefaultBillingAddressId` and
  `AppUser.DefaultShippingAddressId` store personal defaults. On replace,
  the previous personal defaults follow the updated address; on unlink,
  the defaults are cleared. Smartstore's existing SetDefaultAddress action
  uses the AppUser settings for new sessions.
- Smartstore's address mapping is prevented from updating the legacy
  Customer default-address attributes for AppUser sessions. Its delete
  action is now an anti-forgery-protected POST.
- New isolated tests verify that anonymous requests cannot create/delete/
  replace or set another person's defaults, and nop's tests assert that
  creating an address copies data and links the authenticated AppUser.
- User-accessible buyer-order listing is additionally constrained to the
  active storefront to avoid accidental cross-store disclosure.

### Important remaining checkout integration

The personal default address fields are **not yet wired through every
legacy checkout step**. Old checkout shipping/billing selections and price,
tax, loyalty and payment assumptions may still use the legacy Customer
record even when the final Order.CustomerId is the chosen commercial buyer.
Full checkout and payment migration/reconciliation is mandatory before
releasing the independent-user flow.

No migrations are included in this PR. Build and tests are not executed.
