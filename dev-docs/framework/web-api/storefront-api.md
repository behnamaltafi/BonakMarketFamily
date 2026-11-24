# Storefront REST API — Native First (phase 1)

## Architecture decision
BonakMarket.StorefrontApi is an in-process module; it is not a separate commerce service.
Controllers return ASP.NET Core ActionResult<T>, HTTP status codes and ProblemDetails.
CatalogFacade and CartFacade orchestrate existing native commerce services; Identity owns account/profile, OTP, token issuance, validation and revocation; the Query classes contain only customer-facing projections and ownership queries. No generic repository, no Common package or private NuGet feed is required. OData controllers and the existing storefront remain unchanged.

Catalog uses ICatalogSearchService.PrepareQuery and CatalogSearchQuery, with the native LINQ search visitors and IPagedList<T>. A strict database base scope keeps ACL/store/visibility conditions mandatory, even when native settings can bypass them. This deliberately uses the native LINQ provider rather than SearchAsync with a potentially stale external index: the count and returned data are computed from the same scoped source. Index, facets and search events are not enabled on this route yet. Product pricing uses IPriceCalculationService and ProductBatchContext; price filters/sorting follow native catalog search price semantics, not guaranteed customer-specific final calculated price. Native order and cart services remain authoritative.

## Build and installation
```powershell
git switch feature/storefront-api
dotnet restore BonakMarket.sln
dotnet build BonakMarket.sln --no-restore
dotnet test test/BonakMarket.Core.Tests --no-build --filter FullyQualifiedName~StorefrontContractTests
```
Install MultiSms, then Identity, then StorefrontApi in the module manager. On an existing installation keep MultiSms and StorefrontApi installed; install Identity before exposing customer traffic and restart. Identity owns the historical OTP and session migrations and retains their versions and table names. Its installation imports legacy global login switches without overwriting existing Identity settings. Do not reset the database. No Common packages or Nexus configuration needed. EF Core and SQL Client versions are back to develop versions; JWT bearer remains referenced for the auth dependencies.

## Authentication / authorization
Use HTTPS (behind a proxy configure trusted forwarded headers).
Cookie: native sign-in. GET /api/identity/v1/auth/session returns a CSRF token/header; use it with the cookie on mutations.
Mobile: set MobileApi__Enabled=true and MobileApi__SigningKey (base64 of at least 32 random bytes, from secret storage). 
1. GET /api/identity/v1/auth/session
2. POST /api/identity/v1/auth/sms/request {phone}; keep cookie and returned CSRF header.
3. POST /api/identity/v1/auth/sms/verify {phone,code,issueTokens:true}; returns accessToken, refreshToken, expiresIn. Existing SMS response unchanged.
4. Send Authorization: Bearer <accessToken> on /api/storefront/v1 endpoints.
5. POST /api/identity/v1/auth/refresh {refreshToken}; replace both credentials, do not refresh concurrently.
6. POST /api/identity/v1/auth/logout (Bearer revokes its session; cookie requires CSRF and signs out the native cookie). Returns 204.
Access expiry up to 10 minutes, refresh absolute lifetime 30 days. No Basic fallback. Identity authentication middleware sets the verified identity before WorkContext for both account and storefront routes. It never authenticates OData, admin, or MVC routes; request/verify retain cookie CSRF and refresh accepts its own credential. JWT issuer is BonakMarket, audience BonakMarket.CustomerApi; sub is CustomerId, with sid/jti/iat/exp. Previously issued access JWTs must be refreshed or reissued after the audience change. Authorization filter validates identity, native permissions, HTTPS and cookie antiforgery. Ownership checks use the resolved customer and store; there is no client-supplied CustomerId.
Native permission provider registers storefront.catalog (guest/registered), storefront.cart, storefront.orders, and storefront root (administrator). Existing AccessShop, DisplayPrice, AccessShoppingCart remain in force. Verify these assignments in admin after install. A role cannot use permissions to bypass order/address ownership.

## REST endpoints
All paths below use /api/storefront/v1. Catalog is available to guests with native permissions.
- GET /products?search=milk&categoryId=12&brandId=4&minPrice=10&maxPrice=100&inStock=true&hasDiscount=true&sort=PriceAsc&page=1&pageSize=20&includePrice=true
- GET /products/{id}
- GET /products/{id}/price?quantity=1
- GET /products/{id}/variants
- GET /categories?parentId=0&search=food&page=1&pageSize=20
- GET /brands?search=name&page=1&pageSize=20
- GET /orders?page=1&pageSize=20&orderStatusId=10 (authenticated)
- GET /orders/{id}; POST /orders/{id}/reorder (native reorder can skip unavailable items; re-read cart)
- GET /cart; GET /cart/total
- POST /cart/items {productId:123,quantity:2,variants:[{variantAttributeId:1,valueId:2}]}
- PUT /cart/items/{id} {quantity:3}; DELETE /cart/items/{id}
- POST /cart/coupon {code:"..."}; DELETE /cart/coupon (204 on success)
 
## Account endpoints (Identity)
The canonical routes are versioned and divided by responsibility. The old routes remain compatibility aliases to the same actions and retain response bodies. Private actions accept valid customer Bearer or native authenticated cookies. Cookie mutations require CSRF; fetch a fresh session after signing in. All private account operations require HTTPS and reject identity/WorkContext mismatch and impersonation. No customer ID can be supplied to read or edit another account.

| Controller | Method | Canonical path | Legacy alias |
| --- | --- | --- | --- |
| SmsLoginApi | POST | /api/identity/v1/auth/sms/request | /api/sms-login/request |
| SmsLoginApi | POST | /api/identity/v1/auth/sms/verify | /api/sms-login/verify |
| AuthApi | GET | /api/identity/v1/auth/session | /api/sms-login/session |
| AuthApi | POST | /api/identity/v1/auth/refresh | /api/sms-login/refresh |
| AuthApi | POST | /api/identity/v1/auth/logout | /api/sms-login/logout |
| ProfileApi | GET | /api/identity/v1/me/profile | /api/sms-login/profile |
| ProfileApi | PUT | /api/identity/v1/me/profile | /api/sms-login/profile |
| ProfileApi | GET | /api/identity/v1/me/addresses | /api/sms-login/profile/addresses |
| AccountSecurityApi | POST | /api/identity/v1/me/security/phone/request | /api/sms-login/profile/phone/request; /api/sms-login/enroll/request |
| AccountSecurityApi | POST | /api/identity/v1/me/security/phone/verify | /api/sms-login/profile/phone/verify; /api/sms-login/enroll/verify |

Legacy POST /api/sms-login/profile is also retained with its original completion response. New callers should use PUT /api/identity/v1/me/profile. Address reads and profile reads are independent of OTP availability for an otherwise valid authenticated session. The existing phone-first profile-write policy remains: a verified phone and enabled OTP/gateway are required. The token service retains its existing OTP-enabled session policy. General profile editing for password-only customers, address CRUD and password/email security changes are not introduced by this refactor. The native MVC password/customer flow is unchanged. Session responses retain the existing enabled, profileRequired, phone, isAuthenticated and CSRF fields for compatibility.

The CustomerProfileService now owns phone-linked profile reads and profile completion; SmsOtpLoginService no longer implements profile mutation and uses the shared profile contract to read onboarding state. Account controllers share a single CustomerAccountApiController security boundary. Phone enrollment remains an authenticated security operation, not a login operation. Browser and mobile credentials remain separate; signing out one does not automatically revoke the other.

Identity Swagger lists canonical Identity paths only. Storefront Swagger lists canonical commerce paths only. Legacy aliases continue to work but are omitted from both documents. OData documentation and authentication do not include customer routes.

## OData boundary
WebApi Startup registers Basic authentication with a public/secret API key for the integration API. No mobile JWT or anonymous access is enabled on OData here. Countries, states, currencies, languages, units and other reference data are reuse candidates only after verifying the individual endpoint, read-only contract, field/navigation allowlist, publication/store/ACL, and bounded query options. Do not embed an integration API secret in a customer application. Brands and categories stay in StorefrontApi because their actual queries depend on customer roles and store mappings, even for guest traffic. No OData endpoint or permission changes. Its Swagger JSON/YAML route is now restricted to the native integration document names.

Source review: Countries and StateProvinces require Configuration.Country.Read; Currencies require Configuration.Currency.Read; QuantityUnits requires Configuration.Measure.Read; Languages requires Configuration.Language.Read. ProductTags requires Catalog.Product.Read. Their collection actions return entity queries using Entities.AsNoTracking(), rather than an explicitly public DTO. Categories and Manufacturers likewise expose entity queries and require their catalog read permissions, and expose media and discount navigation endpoints. None of these reviewed actions is approved for direct anonymous customer access in this phase. This is a source inspection, not a live endpoint or full EDM audit. A future public read-only surface can reuse the underlying native query/services and explicitly restrict fields, filters, navigation, paging and publication rules without changing the integration API.

Breaking changes against the unmerged earlier implementation: /products/search, /categories/search, /brands/search, /orders/search POST routes are replaced by GET collection routes, and dynamic Common filter bodies and ApiResult envelope are removed. Successful list responses are {items,page,pageSize,totalCount,totalPages}. Page size 1..100, page 1..10000. Errors use actual HTTP status plus ProblemDetails. No dynamic SQL fields, raw entity responses, export or aggregates.

## OpenAPI / Swagger

| UI | Document URL | Owner |
| --- | --- | --- |
| /docs/customer | Uses the enabled JSON URLs below | Identity hosts a shared UI; no extra JSON route |
| /docs/identity | /docs/identity/identity/swagger.json | Identity |
| /docs/storefront | /docs/storefront/storefront/swagger.json | StorefrontApi |
| /docs/api | Existing native OData JSON/YAML URLs | WebApi |

Customer documentation is enabled in Development. Other environments opt in independently with Identity__EnableSwagger=true and StorefrontApi__EnableSwagger=true (restart required). A disabled module's UI and JSON return 404; the shared UI lists only enabled, installed modules. It discovers Storefront through the native module catalog rather than module-local Swagger options, because private Swagger assemblies can be isolated by the module loader. The OData JSON/YAML middleware accepts only the native integration document names. Customer documentation remains separate from Basic integration credentials. Flags enable documentation, not per-user authorization; use a reverse proxy access policy / private network if enabling in Production. No persistent token storage is configured in the customer UI. Cookie login requires the same browser's cookies and CSRF token; Swagger cannot set an HttpOnly cookie directly.

The Identity group metadata is explicit so the WebApi MVC convention does not hide the new controllers. The old aliases are not listed. Operation IDs are deterministic based on method and canonical route. Identity success response DTOs preserve the existing JSON shape, including the absence of token fields for cookie-only sign-in. Some legacy errors retain code-based or empty bodies; do not assume every error is ProblemDetails. Storefront documents ProblemDetails responses and 204 coupon/reorder operations. The existing OData document filter is scoped to integration documents.

Validation on staging: check both JSON documents, verify their path sets, tags, response schemas and security requirements, check that OData URLs cannot serve customer documents, verify each flag combination outside Development, and exercise SMS request/verify with CSRF plus JWT and cookie requests. The Swagger contract tests are in SwaggerContractTests; runtime validation remains required.
 
## Boundaries and validation
- Simple products and list-based variants (dropdown, radio, boxes, checkbox) are supported via native attribute materializer. Bundles, grouped products, gift cards, user-entered price, text/date/file attributes are not fully supported. File GUIDs are not accepted.
- Checkout, payment, shipping selection, order creation, wishlist, address editing, CMS, recommendations and facet discovery remain future work.
- Native product details are projected to dedicated DTOs; MVC helpers are not called because they depend on UI and HTTP form state. Localization of catalog names and full native view model parity remain follow-up work.
- No SDK or running SQL Server was available in the editing environment. Test source was added but no build, install or test was executed. Before merge verify restore, install, migrations, permission registration, anonymous guest catalog, JWT/cookie identity, CSRF, token rotation, revoked tokens, stale role/ACL changes, cross-customer/store resource requests, variant validation, pricing and coupon totals with SQL Server. Measure search latency before deciding whether to enable an index provider.

## Module split
- `BonakMarket.Identity`: native Customer authentication, OTP generation/validation and SQL quotas, profile, MVC login, JWT, refresh and account endpoints. No compiled reference to MultiSms; consumes Core `ISmsSender` and `ISmsDeliveryAvailability`.
- `BonakMarket.MultiSms`: providers, credentials, runtime routing/failover, delivery history and admin test. No account controllers or identity state. Its own `Enabled` switch controls transport availability.
- `BonakMarket.StorefrontApi`: commerce-only routes, authorization, DTOs and native adapters. Module dependencies: StorefrontApi -> Identity -> MultiSms; no direct implementation assembly references.
- Configure OTP and primary login in the Customer Identity module configuration page; provider settings remain in MultiSms. Legacy global switches are seeded at Identity installation, and old setting rows are not deleted. Old `/sms-login` and `/api/sms-login` routes, JWT signing configuration, issuer/audience, Data Protection purpose, and SQL names are preserved. Existing sessions and challenges should remain compatible if keys and settings are preserved; verify against an upgraded staging database.
- Clean stale module build/publish output (especially old MultiSms login views and DLL) without deleting App_Data, key rings or SQL data. Do not uninstall MultiSms to upgrade. Verify installation order, settings migration, fresh/upgrade SQL migration behavior, no duplicate routes, MVC and mobile login, refresh, phone enrollment, cart/orders, and disabling the gateway. No live migration or build executed in this editing environment.


## Customer onboarding

See [Customer onboarding and address management](customer-onboarding.md) for the current Identity v1 contracts, guild setup, checkout restrictions and phone-change workflow. Native customer address actions delegate to the unified profile page when Identity is installed.
