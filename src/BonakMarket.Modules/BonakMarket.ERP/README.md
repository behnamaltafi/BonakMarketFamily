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
- Login: /api/auth/login (42), Token and expiresAt. Exact login JSON field names and Authorization scheme are not specified. Supply the exact login body from a working Swagger/Postman sample; do not guess. OData prefix, each read operation endpoint, HTTPS URL and authorization scheme configurable; use an empty scheme for a raw token if confirmed. The default odata/ and Bearer are assumptions, not guaranteed by the document. Token is obtained per operation and never stored or returned.
- Read requests use GET, bounded $skip/$top and $orderby=Id, accepting standard value envelopes or arrays and same-base next links. Confirm the deployed OData version and pagination against real fixtures.

## Safety and scope
Connections default disabled. Company code + connection code identify a connection; a product external ID is unique within a company. One local product may belong to only one staged ERP product (filtered SQL unique index). Explicitly link an existing BonakMarket product by ID or create an unpublished, purchase-disabled draft with a selected product template. SKU conflicts (including variants and deleted products) require manual mapping; there is no auto-match by SKU, no auto-publish and no deletion based on an incomplete feed. Product staging is a repeatable atomic bounded page import with execution history. Each product's canonical snapshot is saved, including pack and public price. Contract prices and stock are read-only previews; stock warehouse ID 0 means aggregated and must not be added to warehouse rows. Negative price sentinel becomes null; date strings are preserved. No contract price overwrites public product price. The PDF does not define money unit or final pricing precedence. Configure egress rules for the ERP host; only trusted admins may set connection URLs. HTTPS, no redirects, same-origin next links, bounded response, bounded timeout; persist shared Data Protection keys. Never log request/response credentials. Disable login only when the ERP administrator has explicitly allowed unauthenticated access.

## Not yet enabled
Scheduled sync, automatic per-operation connection routing and live price/stock application, customer/order/receipt writes, reconciliation and retry queue require subsequent phases. Varanegar CreateOrder supports ActionType 1 (request only) and 5 (request + special invoice), plus free-invoice actions in revision history. Stable ReferenceKey, multiple external order IDs, Persian date, smallest unit quantities and MessageType semantics must be implemented before writes. Do not use a live write as a test.
No new dependencies. Build and validate the migration on a disposable SQL Server database before deployment. Real endpoint fixtures, authentication parameters and currency/unit/warehouse policies are required before production rollout.

## Tests
Provider contract tests are in BonakMarket.Core.Tests/ERP. Run `dotnet test test/BonakMarket.Core.Tests/BonakMarket.Core.Tests.csproj --filter FullyQualifiedName~VaranegarProviderTests` before use. Tests use a fake HTTP handler, not live credentials.
