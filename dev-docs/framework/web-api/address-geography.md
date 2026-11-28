# Address geography — native reuse audit

| Requirement | Native symbol/source | Decision | Verification |
|---|---|---|---|
| Iran/provinces | Core/Common/Domain/Country.cs, StateProvince.cs; InstallationDataSeeder | Reuse ISO country, existing province, installation seed; add Tehran city idempotently | SQL install and upgrade, repeated seed |
| City | SmartDbContext.Common.cs | Extend native domain with City and FK; no parallel province table | SQL FK and uniqueness |
| Address | Identity/Services/CustomerAddressService.cs, CustomerAddressApiController | Extend owned-address CRUD and settings; legacy text remains; no external location validation | contract tests, API integration |
| Coordinates | Core/Common/Domain/Address.cs | Nullable decimal fields because native Clone snapshots order addresses | AddressTests, order integration |
| Checkout | StorefrontApi/Services/CheckoutAddressChecks.cs | Extend existing fingerprint | checkout regression |
| Orders | StorefrontApi/Queries/OrderQueries.cs | Extend owned order DTO | contract tests |
| Admin | CountryController and native country edit view | Extend same permission boundary with city management; display coordinates in shared admin editor | MVC authorization, CRUD and snapshot regression |

No map vendor or province polygon dependency. Public locations only return published Iranian data. Existing address text and IDs remain intact. Coordinate omission preserves existing data and explicit null pair clears it. The backend only checks coordinate shape/range, not location membership.

Validation gate: build and run tests against installed SQL Server for migration/seed, scoped city lists, CRUD and snapshot isolation; no runtime success is implied by source changes.
