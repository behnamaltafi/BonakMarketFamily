# Storefront release checklist
Branch feature/storefront-api. Source review is not a successful build or a production approval.
Keep StorefrontApi:EnableOfflineCheckout false until SQL Server and native browser concurrency tests pass.

## Automated validation
Use the existing .NET 10 toolchain:
```sh
dotnet build BonakMarket.sln -c Release -p:BonakMarketBuildRuntime=linux-x64 --no-self-contained
dotnet test test/BonakMarket.Core.Tests/BonakMarket.Core.Tests.csproj -c Release -p:BonakMarketBuildRuntime=linux-x64 --no-build --no-restore --filter "FullyQualifiedName~Platform.Messaging|FullyQualifiedName~OrderPlacementSafetyTests"
```
On Windows use win-x64. Use SQL Server staging for provider-specific queries and migrations;
in-memory/SQLite contract tests are not substitutes.

## Required end-to-end release checks
- Build entire solution, Swagger route/security tests, native web login, legacy CSRF, token-only mobile OTP, refresh/logout.
- Customer/store isolation and inactive/deleted customer; no price leaks to roles without DisplayPrice.
- Product details/variant validation/quote, facets and SQL query translation, localized CMS ACL/password/widget exclusions.
- Active/inactive cart, bundle/required product behavior, price rounding, coupon/gift/reward calculations, cart revision,
  credit balance fallback (estimate null), unavailable products, concurrent changes.
- Order historical amounts, shipment/return/download/review permissions and customer isolation.
- Migration preflight: duplicate OrderGuid including deleted orders; migration and rollback on copied SQL Server data.
- Offline opt-in: real owned addresses, fresh shipping rates, native total and accepted currency, legal/custom checkout fallback,
  duplicate confirmation from two instances, identical-key retry, changed payload conflict, native order lookup after lost
  response, uncertain attempt reconciliation, native provider configured transaction status, zero amount and minimum interval,
  simultaneous native MVC checkout/cart mutation, rollback and mail/event failure. Keep disabled on failure.
- Online payment: no headless provider integration is shipped. A chosen provider must be audited for idempotency,
  redirect/callback validation, authoritative verification and recovery before enabling. PDF remains native browser-only.
- OData integration regression: authorization scheme and Swagger paths unchanged.
- Confirm Jenkins build/deploy pipeline remains unmodified; deploy only after approval.
