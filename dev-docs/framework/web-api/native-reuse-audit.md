# Native Reuse Audit — required engineering checklist

Use this before coding, again before merge. The root AGENTS.md is authoritative.

1. State acceptance criteria, bounded modules, and explicit non-goals.
2. Read relevant dev-docs; inspect the target branch. Enumerate source folders if search is unavailable.
3. Trace native admin and storefront UI, services/interfaces, query extensions, entities, settings, permissions, events/providers, migrations, localization and tests. Inspect related modules and ERP contracts. Search synonyms, not only proposed names.
4. Fill this matrix in the PR or implementation notes. A missing result is not proof of absence.

| Requirement | Native source path and symbol | Decision | Gap / justification | Verification |
|---|---|---|---|---|
| ... | ... | Reuse / Adapter / Extend / New | ... | ... |

5. Prefer reuse, then thin adapter, then supported extension; new persistent state only if there is no authoritative native owner. Request approval before changing Core or expanding scope.
6. Verify final diff against ownership boundaries and the matrix; build, run tests and check real provider queries, permissions, settings persistence, DTO semantics and backward compatibility. Record blockers accurately.

## Storefront phase 1 audit

| Requirement | Native symbol / source | Decision and gap |
|---|---|---|
| Catalog search & paging | Core/Catalog/Search/CatalogSearchQuery.cs; ICatalogSearchService.PrepareQuery; StorefrontApi/Queries/NativePaging.cs | Reuse scoped native query and paging. |
| Bestseller ranking | Core/Checkout/Orders/Extensions/OrderItemQueryExtensions.cs: SelectAsBestsellersReportLine; Web/Components/HomeBestsellersViewComponent.cs | Reuse aggregate; strict storefront visibility, 90-day order eligibility and stable ID tie-break are explicit customer-facing policy. |
| Personal purchase frequency | Core/Checkout/Orders/Extensions/OrderQueryExtensions.cs: ApplyStandardFilter; OrderItemQueryExtensions.cs: SelectAlsoPurchasedProductIds | Reuse owned order scope; distinct order count for one customer differs from also-purchased and bestseller reports, so add only this projection. |
| Homepage display | Core/Catalog/Search/CatalogSearchQuery.cs: HomePageProductsOnly; Core/Catalog/Products/Domain/Product.cs: ShowOnHomePage, HomePageDisplayOrder; Web/Components/HomeProductsViewComponent.cs; Core/Catalog/Configuration/CatalogSettings.cs | Reuse native flags and order; expose native bestseller layout preferences in bootstrap. |
| Retail price and saving | Core/Catalog/Pricing/Services/PriceCalculationService.cs: DetectComparePrices, CreateCalculatedPrice; Domain/PriceLabel.cs: IsRetailPrice | Reuse calculated RetailPrice and Saving; no parallel pricing spec, currency setting or price formula. |
| Packaging | Core/Catalog/Attributes/Domain/SpecificationAttributeOption.cs: NumberValue; Core/Common/Domain/QuantityUnit.cs; Product.cs: QuantityPerParentUnit is for required products, not packaging; native BasePriceMeasureUnit is a weight/dimension measure (Web/Areas/Admin/Controllers/ProductController.cs: AvailableMeasureUnits), not the product sale quantity unit | Reuse numeric spec and explicit native package unit. Only derive per-item reference when both are known. |
| Admin settings | Web.Common/Modelling/Settings/LoadSettingAttribute.cs, SaveSettingAttribute.cs; Identity/Controllers/CustomerIdentityAdminController.cs | Reuse ISettings, global load/save, native admin permission and antiforgery; no appsettings keys or tables. |

Verification: HomeCatalogTests, WholesaleCardAdminTests, StorefrontSearchContractTests, SwaggerContractTests, HomeCatalogSqlServerTests (relational translation and opt-in read-only provider smoke); SQL Server application integration and build must pass before merge. A native MVC setting controlling homepage visibility does not disable a generic catalog sort. Treat all calculated reference prices as display metadata, not cart authority.
