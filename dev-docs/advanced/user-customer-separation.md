# User / Customer separation — phase 1 design and progress

## Target ownership

- **AppUser** represents an authenticated individual and owns credentials,
  phone/email, roles, permissions, addresses, shopping cart, wishlists,
  loyalty/reward/wallet transactions, reviews, activity, privacy and other
  personal data.
- **Customer** represents the buyer/commercial account on an order. Among
  personal/business transactions, only **Order.CustomerId** identifies the
  commercial Customer.
- **AppUserCustomer** represents the explicit many-to-many authorization
  relationship between users and customers.
- **Order.PlacedByAppUserId** records the authenticated person who placed the
  order; it does not replace the buyer in Order.CustomerId.
- Catalogs, stores and other shared data are not personal AppUser records.

## Source changes in this branch

- New AppUser fields: stable UserGuid plus Username, Email, PhoneNumber and
  PhoneVerified.
- Membership models: AppUserCustomer, AppUserRoleMapping and
  AppUserAddressMapping with EF model configuration.
- Auth/context seams: ICurrentAppUserContext (strict app_user_guid claim),
  IAppUserCustomerService, IAppUserOrderAccessService and
  IAppUserProfileService; registered in Autofac.
- Nullable AppUserId transition fields on ShoppingCartItem,
  BackInStockSubscription, CustomerContent (reviews/helpfulness),
  ExternalAuthenticationRecord, RewardPointsHistory, WalletHistory,
  ActivityLog and ReturnCase.
- Order.PlacedByAppUserId remains separate from Order.CustomerId.
- NUnit tests assert invalid, missing and legacy Customer claims cannot
  authenticate an AppUser.

## Compatibility limit: existing runtime is NOT separated

The core still registers `AddIdentity<Customer, CustomerRole>()`,
`UserStore<Customer>` and `SmartSignInManager`. Legacy controllers, work
context and services continue to read CustomerId for carts, address book,
roles, reviews, reward points and checkout. The new nullable AppUserId
properties are currently not consumed by those old workflows.

**Do not merge/deploy this branch as a completed user/customer split.**
Required remaining cutover:

1. Provision verified AppUser identities and migrate existing personal records
   without confusing a CustomerId and AppUserId.
2. Change ASP.NET Core Identity, sign-in manager, user store, user claims and
   work context so authentication resolves AppUser, not Customer.
3. Refactor carts, addresses, role/permissions, external logins, wallet,
   rewards, reviews, customer content, return cases and all non-order user
   services/controllers to AppUser ownership.
4. Introduce authenticated AppUser plus independently selected Customer
   throughout order creation and access. Membership must be checked for every
   customer-scoped order operation; recalculate commercial totals if buyer
   changes.
5. Preserve historical Order.CustomerId. Store PlacedByAppUserId for audit.
6. Remove legacy Customer-as-user dependencies only after data migration and
   behavior/security tests.

## No migration scripts in this phase

The repository owner will write the FluentMigrator migration(s) **at the
end**. No new migration class or install-schema modification belongs to this
branch. Model and DbContext changes only describe the desired schema.

Final migration must create identity/link tables, unique constraints for
UserGuid and membership pairs, indexes/FKs, new user ownership columns,
validated backfill, and safe retirement of legacy user ownership.

Build, identity security tests, DB migration smoke tests and checkout
regression tests must pass before deployment. No build or database test has
been run here. SMS/OTP multi-provider work is a separate future phase.
