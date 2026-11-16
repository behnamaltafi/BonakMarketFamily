# Phase 1: Separate users from commerce customers

The new `AppUser` entity models an independent application principal, while
`Customer` remains the commerce entity referenced by orders, carts and addresses.

`AppUserCustomer` is an explicit many-to-many membership with a unique pair
constraint. `IAppUserCustomerService` provides membership administration and
authorization checks. The caller must use an authenticated user ID for queries
and must independently authorize administration operations.

This is an **additive foundation, not an authentication cutover**:
- Existing `AddIdentity<Customer, CustomerRole>()`, `SmartSignInManager`,
  `UserStore`, and checkout work-context behavior remain unchanged.
- Do not assume that an authenticated legacy `Customer` ID is an `AppUser` ID.
- Existing customers are not automatically mapped to users; phone/email equality
  must not implicitly grant access to a customer.
- Future cutover must provision users, migrate/verify memberships, establish
  an authenticated AppUser principal, resolve the selected authorized customer,
  and adapt the current-work-context and customer-dependent authorization.
- SMS, OTP and provider selection are not part of this phase.

Apply and smoke-test the FluentMigrator migration on a disposable database before merging.
