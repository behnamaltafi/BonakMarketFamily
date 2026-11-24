# Storefront REST API (phase 1)

The independent module BonakMarket.StorefrontApi serves /api/storefront/v1. OData and the MVC storefront are unchanged. Install MultiSms and StorefrontApi through the module manager. The native SmartDbContext, shopping cart, order and pricing services remain authoritative.

## Build / deployment

Restore Common.Contracts 1.0.1 and Common.Paging.EntityFrameworkCore 1.0.0 from the organization's NuGet feed. Common.Data v1.0.0 declares the Foundation dependency range [1.0.1,2.0.0). Verify the actual released packages exist in Nexus; no credentials are committed. Supply BONAK_COMMON_NUGET_SOURCE as a non-secret feed URL, or configure the agent's NuGet sources. Version properties StorefrontCommonVersion and StorefrontPagingVersion can be overridden. The host EF Core references and SQL client are aligned to 10.0.12 and 7.1.0 respectively to avoid a second EF runtime in the module.

Run:
```powershell
$env:BONAK_COMMON_NUGET_SOURCE = 'https://YOUR-NEXUS/repository/nuget-hosted/index.json'
dotnet restore BonakMarket.sln
dotnet build BonakMarket.sln --no-restore
dotnet test test/BonakMarket.Core.Tests --no-build --filter FullyQualifiedName~StorefrontPagingTests
```

Use HTTPS. When running behind a reverse proxy, configure trusted forwarded headers so ASP.NET sees HTTPS. Existing installation database requires the new MultiSmsMobileApiSession migration; never drop the database.

## Authentication

Cookie remains the native website login. Obtain CSRF request token/header with GET /api/sms-login/session, keep cookies, send the returned header for POST/PUT/DELETE. Read-only public catalog search POSTs do not require CSRF because they do not mutate state.
For mobile, configure MobileApi__Enabled=true and MobileApi__SigningKey (base64 encoding of at least 32 random bytes, stored outside Git). 
1. GET /api/sms-login/session
2. POST /api/sms-login/request with phone, session cookie and CSRF header.
3. POST /api/sms-login/verify with phone, code, issueTokens:true; preserve CSRF header. Response includes accessToken, refreshToken, expiresIn.
4. Send Authorization: Bearer <accessToken> for storefront calls. The validated ticket is installed before WorkContext. No Basic fallback.
5. POST /api/sms-login/refresh with refreshToken; rotate and replace both credentials; do not send concurrent refreshes.
6. POST /api/storefront/v1/auth/logout with Bearer to revoke the session, or cookie + CSRF to sign out the native website.
Access tokens last up to 10 minutes; absolute refresh expiry is 30 days. OTP controllers retain the existing response format for compatibility, while new Storefront actions use Common.ApiResult.

## Phase 1 endpoints

Public catalog (guest permissions and ACL/store restrictions still apply):
- POST /api/storefront/v1/products/search?categoryId=1&brandId=2&includePrice=true (optional parameters)
- GET /api/storefront/v1/products/{id}
- GET /api/storefront/v1/products/{id}/price?quantity=1
- POST /api/storefront/v1/categories/search
- POST /api/storefront/v1/brands/search

Authenticated:
- GET /api/storefront/v1/profile
- PUT /api/storefront/v1/profile (verified-phone account profile completion; native service)
- GET /api/storefront/v1/profile/addresses (read only)
- POST /api/storefront/v1/orders/search
- GET /api/storefront/v1/orders/{id}
- POST /api/storefront/v1/orders/{id}/reorder (native reorder can skip unavailable items; re-read cart)
- GET /api/storefront/v1/cart
- GET /api/storefront/v1/cart/total
- POST /api/storefront/v1/cart/items { "productId": 123, "quantity": 2 }
- PUT /api/storefront/v1/cart/items/{id} { "quantity": 3 }
- DELETE /api/storefront/v1/cart/items/{id}
- POST /api/storefront/v1/cart/coupon { "code": "..." }
- DELETE /api/storefront/v1/cart/coupon
- POST /api/storefront/v1/auth/logout

Swagger: /docs/storefront (development; opt in outside development with StorefrontApi__EnableSwagger=true). The document is independent of the OData groups; it includes the existing SMS authentication actions.

## Common paging contract

Example body for products/search:
```json
{
  "pageNumber": 1,
  "pageSize": 20,
  "sortingParams": [{"columnName":"Name","sortOrder":1}],
  "filterParam": [{"columnName":"Name","filterValue":"milk","filterOption":9}],
  "comparisonMode": 1,
  "outputType": 1
}
```
Use Common.Paging enums; 1 = Asc, 2 = Desc; Equal=5, Contain=9. Search applies owner/visibility constraints first, projects to DTO, then invokes Common.Paging.FilterPaging. Page size is capped at 100; no export, aggregates, full-text or arbitrary navigation fields. For a one-item page the native PagingFilter setter normalizes page size to 10. Product price is not a sortable SQL field; calculated discounts depend on the customer, currency, quantity and native pricing options. OrderTotal is the persisted primary-currency order total, not a converted display total. Cart total can be null until shipping/payment information is available.

## Phase 1 boundaries / verification

- Cart addition supports simple products without variant configuration; grouped/bundle/gift card/customer-entered-price products and custom attributes require further DTOs and native integration.
- Profile modification is the existing verified-SMS profile flow; address editing and phone change via mobile bearer are not included. The legacy cookie enrollment endpoints remain available.
- Checkout, payment, shipping selection, order creation, wishlist, CMS, recommendations, dedicated promotions discovery and advanced product filtering are later phases.
- No API key is issued to a customer. Explicit order/cart customer and store filters are not bypassable by the supplied paging query. No OData source changes are necessary.
- Test the installation/migration, package restore, Swagger, guest ACL/store isolation, expired/rotated/revoked tokens, cookie CSRF, cross-customer orders/cart including guessed IDs, pricing and coupon application before merging. Unit tests cover paging validation only. End-to-end verification has not yet been executed.
