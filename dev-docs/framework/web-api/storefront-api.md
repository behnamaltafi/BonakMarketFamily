# Storefront REST API — Native First (phase 1)

## Architecture decision
BonakMarket.StorefrontApi is an in-process module; it is not a separate commerce service.
Controllers return ASP.NET Core ActionResult<T>, HTTP status codes and ProblemDetails.
CatalogFacade and CartFacade orchestrate existing native commerce services; Identity owns account/profile, OTP, token issuance, validation and revocation; the Query classes contain only customer-facing projections and ownership queries. No generic repository, no Common package or private NuGet feed is required. OData controllers and the existing storefront remain unchanged.

Catalog uses ICatalogSearchService.PrepareQuery and CatalogSearchQuery, with the native LINQ search visitors and IPagedList<T>. A strict database base scope keeps ACL/store/visibility conditions mandatory, even when native settings can bypass them. This deliberately uses the native LINQ provider rather than SearchAsync with a potentially stale external index: the count and returned data are computed from the same scoped source. Index, facets and search events are not enabled on this route yet. Product pricing uses IPriceCalculationService and ProductBatchContext; price filters/sorting follow native catalog search price semantics, not guaranteed customer-specific final calculated price. Native order and cart services remain authoritative.

## Build and installation
```powershell
git switch feature/customer-api-jwt-only
dotnet restore BonakMarket.sln
dotnet build BonakMarket.sln --no-restore
dotnet test test/BonakMarket.Core.Tests --no-build --filter FullyQualifiedName~StorefrontContractTests
```
Install MultiSms, then Identity, then StorefrontApi in the module manager. On an existing installation keep MultiSms and StorefrontApi installed; install Identity before exposing customer traffic and restart. Identity owns the historical OTP and session migrations and retains their versions and table names. Its installation imports legacy global login switches without overwriting existing Identity settings. Do not reset the database. No Common packages or Nexus configuration needed. EF Core and SQL Client versions are back to develop versions; JWT bearer remains referenced for the auth dependencies.

## Authentication / authorization
Use HTTPS (behind a proxy configure trusted forwarded headers).
Customer API authentication is JWT-only. Set MobileApi__Enabled=true and MobileApi__SigningKey (base64 of at least 32 random bytes from secret storage).
1. POST /api/identity/v1/auth/sms/request {phone}; obtain challengeId, expiresIn and resendAfter.
2. POST /api/identity/v1/auth/sms/verify {challengeId,code}; obtain accessToken, refreshToken, tokenType, expiresIn, profileRequired, missingRequiredFields.
3. Send Authorization: Bearer <accessToken> on protected /api/storefront/v1 and /api/identity/v1/me endpoints. Omit the header for guests. Cookie identity is ignored by both APIs.
4. POST /api/identity/v1/auth/refresh {refreshToken}; replace both credentials, do not refresh concurrently.
5. POST /api/identity/v1/auth/logout with valid Bearer and no body, or omit Authorization and supply {refreshToken} to revoke without rotating. Returns 204.

Access expiry up to 10 minutes; refresh absolute lifetime 30 days. The middleware replaces the request principal and authentication feature before WorkContext. JWT issuer BonakMarket, audience BonakMarket.CustomerApi; sub is CustomerId, with sid/jti/iat/exp. Native permissions, HTTPS and customer/store ownership are unchanged. MVC cookie authentication and integration Basic are independent. For a separate web origin configure Identity__AllowedOrigins__0=https://app.example.com (exact origin, no credentials).
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
Identity owns the customer account contract. Private actions require Bearer; no CSRF token or client-supplied customer ID. See [unified customer authentication](mobile-authentication.md) and [customer onboarding](customer-onboarding.md). MVC address/phone widgets use dedicated native cookie+CSRF actions and the same CustomerAddressService and OTP service. Browser and API sessions remain separate; logout of one does not automatically revoke the other. Phone changes revoke all API sessions and invalidate the old verified-phone cookie.

Breaking change: old /api/sms-login/*, /api/identity/v1/auth/session and /api/identity/v1/auth/mobile/sms/* are removed. The canonical /auth/sms/verify accepts challengeId rather than phone/issueTokens. Coordinate existing consumers before merging or deploying.

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

Customer documentation is enabled in Development. Other environments opt in independently with Identity__EnableSwagger=true and StorefrontApi__EnableSwagger=true (restart required). A disabled module's UI and JSON return 404; the shared UI lists only enabled, installed modules. It discovers Storefront through the native module catalog rather than module-local Swagger options, because private Swagger assemblies can be isolated by the module loader. The OData JSON/YAML middleware accepts only the native integration document names. Customer documentation remains separate from Basic integration credentials. Flags enable documentation, not per-user authorization; use a reverse proxy access policy / private network if enabling in Production. No persistent token storage is configured in the customer UI. Customer Swagger documents Bearer only. Login and refresh do not require cookies or CSRF.

The Identity group metadata is explicit so the WebApi MVC convention does not hide the new controllers. The old aliases are removed. Operation IDs are deterministic based on method and canonical route. Identity login returns token-only JSON; canonical errors use ProblemDetails with stable code and optional retryAfter. Storefront documents ProblemDetails responses and 204 coupon/reorder operations. The existing OData document filter is scoped to integration documents.

Validation on staging: check both JSON documents, verify their path sets, tags, response schemas and security requirements, check that OData URLs cannot serve customer documents, verify each flag combination outside Development, and exercise token-only SMS request/verify and reject cookie-only private requests. The Swagger contract tests are in SwaggerContractTests; runtime validation remains required.
 
## Boundaries and validation
- Simple products and list-based variants (dropdown, radio, boxes, checkbox) are supported via native attribute materializer. Bundles, grouped products, gift cards, user-entered price, text/date/file attributes are not fully supported. File GUIDs are not accepted.
- Checkout, payment, shipping selection, order creation, wishlist, address editing, CMS, recommendations and facet discovery remain future work.
- Native product details are projected to dedicated DTOs; MVC helpers are not called because they depend on UI and HTTP form state. Localization of catalog names and full native view model parity remain follow-up work.
- No SDK or running SQL Server was available in the editing environment. Test source was added but no build, install or test was executed. Before merge verify restore, install, migrations, permission registration, anonymous guest catalog, JWT identity isolation, MVC CSRF, token rotation, revoked tokens, stale role/ACL changes, cross-customer/store resource requests, variant validation, pricing and coupon totals with SQL Server. Measure search latency before deciding whether to enable an index provider.

## Module split
- `BonakMarket.Identity`: native Customer authentication, OTP generation/validation and SQL quotas, profile, MVC login, JWT, refresh and account endpoints. No compiled reference to MultiSms; consumes Core `ISmsSender` and `ISmsDeliveryAvailability`.
- `BonakMarket.MultiSms`: providers, credentials, runtime routing/failover, delivery history and admin test. No account controllers or identity state. Its own `Enabled` switch controls transport availability.
- `BonakMarket.StorefrontApi`: commerce-only routes, authorization, DTOs and native adapters. Module dependencies: StorefrontApi -> Identity -> MultiSms; no direct implementation assembly references.
- Configure OTP and primary login in the Customer Identity module configuration page; provider settings remain in MultiSms. Legacy global switches are seeded at Identity installation, and old setting rows are not deleted. Native MVC `/sms-login`, JWT signing configuration, issuer/audience, Data Protection purpose, and SQL names are preserved; legacy API routes are removed. Existing sessions and challenges should remain compatible if keys and settings are preserved; verify against an upgraded staging database.
- Clean stale module build/publish output (especially old MultiSms login views and DLL) without deleting App_Data, key rings or SQL data. Do not uninstall MultiSms to upgrade. Verify installation order, settings migration, fresh/upgrade SQL migration behavior, no duplicate routes, MVC and mobile login, refresh, phone enrollment, cart/orders, and disabling the gateway. No live migration or build executed in this editing environment.


## Customer onboarding

See [Customer onboarding and address management](customer-onboarding.md) for the current Identity v1 contracts, guild setup, checkout restrictions and phone-change workflow. Native customer address actions delegate to the unified profile page when Identity is installed.


## Authenticated storefront expansion

See [Authenticated storefront coverage](authenticated-storefront.md) for the full audit of native login gates, route inventory, exclusions and browser-dependent checkout/payment boundaries. The new controllers share the existing HTTPS/JWT/customer-store authorization filter.
