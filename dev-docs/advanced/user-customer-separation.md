# User / Customer separation — target model and phase 1

## Agreed target ownership

`AppUser` represents an authenticated person independently of any
commercial Customer. Login identifiers, credentials, roles and permissions,
shopping carts, address books, wishlists, profile details, reward points,
reviews and other person-specific data will belong to AppUser.

`Customer` is the commercial buyer/account and is directly associated
with **orders through `Order.CustomerId`**. An order may additionally
capture the user who placed it for auditing; that user is not necessarily
the buyer. Global catalog and store data are not "owned by" an AppUser.

`AppUserCustomer` is the many-to-many permission/membership relationship.
A verified user may place or view an order for a selected Customer **only
after** that relationship is validated. Changing selected Customer must
not change the authenticated principal; if pricing is Customer-specific,
recalculate and validate at checkout.

## Gap in the existing Smartstore runtime

The present core uses `AddIdentity<Customer, CustomerRole>()`,
`SmartSignInManager` and `UserStore : IUserStore<Customer>`. It also
maps `ShoppingCartItem.CustomerId`, Customer addresses, reward points
and roles to the legacy Customer identity. Therefore Customer currently
mixes user and commercial responsibilities; introducing new tables alone
does not separate those behaviors.

The full refactor requires an explicit, backward-compatible migration:
1. Provision independent AppUser identities for legacy people; reconcile
   membership links without guessing from phone/email equality.
2. Move login/session/claims to AppUser instead of Customer.
3. Migrate non-order person-owned relationships to AppUser with new
   foreign keys and staged data backfills; **do not simply rename IDs**.
4. Introduce distinct current-User and selected-authorized-Customer
   contexts; change cart and other person-owned services to use AppUser.
5. Preserve `Order.CustomerId` and historical orders; optionally capture
   `PlacedByUserId` for attribution and explicitly validate membership.
6. Drop legacy Customer-as-user behavior only after tests and safe cutover.

## Delivered in this PR (additive phase 1 only)

- New `AppUser` with a stable `UserGuid`.
- Explicit `AppUserCustomer` many-to-many link with unique-pair constraint.
- FluentMigrator schema migration and SmartDbContext registration.
- `IAppUserCustomerService` with membership management and access checks.

Existing Smartstore Customer authentication and all legacy foreign-key
ownership remain unchanged **for now**, to avoid silently breaking
checkout or historical data. This PR does **not** complete the behavioral
split. OTP and SMS provider changes are not part of phase 1.

Compile, run tests, and smoke-test the database migration on a disposable
database before merge.
