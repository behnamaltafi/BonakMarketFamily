# ERP Integration — phase 1

Install BonakMarket.ERP in the module manager. SQL Server only. Configure from Admin / Settings / ERP Integration.
The implementation deliberately uses one module, existing DI, FluentMigrator, SmartDbContext and admin permissions.
No changes to Core, OData, checkout, or identity.

## Varanegar first provider

Source: supplied Varanegar_ThirdParty_Webservice_01 PDF (64 pages; document has inconsistent revision metadata).
Supported read operations:
- Products: ThirdPartyProductInfoOData (PDF pages 8–9)
- Contract prices: ThirdPartyPriceHistoryOData (10–11)
- Inventory: ThirdPartyStockProductInfoOData (12)
- Warehouses: ThirdPartyStockDCListOData (36)
- Category hierarchy: ThirdPartyProductGroupInfoOData (45)
- Login: /api/auth/login (42), Token and expiresAt. Choose VaranegarLogin or None. Enter username and password separately; both are encrypted together. Wire field names default to username/password; configure exact names based on live API. Optional fiscal year and distribution center values are strings with configurable wire names; confirm their type before use. Tokens are cached per connection/configuration version, with one GET replay on 401. Existing encrypted raw login bodies are migrated without decryption until replaced.
- Read requests use GET, bounded $skip/$top and $orderby=Id, accepting standard value envelopes or arrays and same-base next links. Confirm the deployed OData version and pagination against real fixtures.

## Safety and scope
Connections default disabled. Company code + connection code identify a connection; a product external ID is unique within a company and immutable SourceCode. Use the same SourceCode only when two connections address the same external identity namespace; a different database/year that resets IDs needs a different source. One local product may belong to only one staged ERP product (filtered SQL unique index). Explicitly link an existing BonakMarket product by ID or create an unpublished, purchase-disabled draft with a selected product template. SKU conflicts (including variants and deleted products) require manual mapping; there is no auto-match by SKU, no auto-publish and no deletion based on an incomplete feed. Product staging is a repeatable atomic bounded page import with execution history. Configure a Products route in Operations first. The native ERP task stages one bounded page per due company per tick; checkpoint resets after a short/empty page. Other routes support read-only previews. Offset paging is not a consistent snapshot if ERP rows change mid-run; no missing rows are deleted. Product routes, route changes and connection edits use a company-scoped SQL Server application lock. Connection changes to a different source do not automatically reassign existing local products. The source code migration backfills existing records using the original connection code. Existing installations can register the native task from Operations. Restart/retry replays a page safely; there is no automatic publication. Each product's canonical snapshot is saved, including pack and public price. Contract prices and stock are read-only previews; stock warehouse ID 0 maps to IsAggregate=true and a null WarehouseId; it must not be added to warehouse rows. Negative price sentinel becomes null; date strings are preserved. No contract price overwrites public product price. The PDF does not define money unit or final pricing precedence. Configure explicit deployment allowlist BonakMarket:ERP:AllowedAuthorities:0 (example erp.example.com:8443) and network egress rules for the ERP host; only trusted admins may set connection URLs. HTTPS, no redirects, same-origin next links, bounded response, bounded timeout; persist shared Data Protection keys. Never log request/response credentials. Disable login only when the ERP administrator has explicitly allowed unauthenticated access.

## Not yet enabled
Live price/stock application, cursor/delta sync and full snapshot consistency, customer/order/receipt writes, reconciliation and retry queue require subsequent phases. Varanegar CreateOrder supports ActionType 1 (request only) and 5 (request + special invoice), plus free-invoice actions in revision history. Stable ReferenceKey, multiple external order IDs, Persian date, smallest unit quantities and MessageType semantics must be implemented before writes. Do not use a live write as a test.
No new dependencies. Build and validate the migration on a disposable SQL Server database before deployment. Real endpoint fixtures, authentication parameters and currency/unit/warehouse policies are required before production rollout.

## Tests
Provider contract tests are in BonakMarket.Core.Tests/ERP. Run `dotnet test test/BonakMarket.Core.Tests/BonakMarket.Core.Tests.csproj --filter FullyQualifiedName~VaranegarProviderTests` before use. Tests use a fake HTTP handler, not live credentials.

Provider-specific non-secret settings and authentication options use validated JSON; credentials use separate write-only fields. Add an IErpAuthenticator for a new mechanism without editing the catalog transport. Auth UI for future credential types is not implemented. No new CI workflow; use the existing Jenkins pipeline.

## Deployment configuration
Set `BonakMarket__ERP__AllowedAuthorities__0=erp.example.com` (include a non-default port if necessary) in the application environment. The allowlist fails closed, so no ERP requests work before setup. Deploy with persistent shared Data Protection keys and restrict network egress. Login wire names and date/branch field types must be confirmed; the document names the four login inputs but does not specify their JSON keys. Configure the Products route for manual or scheduled staging. Register the native task for already-installed modules and enable its schedule. No automatic writes to ERP are available.

Connection edits invalidate the token cache key and reset the route checkpoint; old cached tokens expire locally within at most one hour. Concurrent or stale edit forms are rejected. Changes to the external identity namespace require a new connection with a different SourceCode.
