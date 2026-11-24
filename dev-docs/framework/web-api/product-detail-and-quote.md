# Storefront product detail and quote

Existing GET /api/storefront/v1/products keeps the ProductDto list contract and adds sku.
GET /api/storefront/v1/products/{id} returns ProductDetailDto, a superset of ProductDto:
HTML description, public gallery, visible specifications, variant options, merchant preselection, purchase rules,
shipping/preorder indicators, approved review summary, and permission-aware action capabilities.

Images may be relative native media URLs; resolve them against the API origin. Render description HTML only in a
sanitized renderer. Do not infer exact stock from product detail: use the quote and final cart validation.
Purchase quantity limits are merchant limits capped at the existing API limit (1000). Capability flags describe
whether the API operation is supported and permitted, not whether every selection is purchasable.
Some product types, required text/date/file attributes, gift card purchase, customer-entered prices and grouped/bundled
product configuration still require the native UI. No raw attribute strings or upload IDs are accepted.

POST /api/storefront/v1/products/{id}/quote
```json
{"quantity":2,"variants":[{"variantAttributeId":1,"valueId":4}]}
```
The public catalog authorization boundary remains in force, including HTTPS and the existing CSRF requirement for
non-bearer POSTs. Logged-in mobile clients use Bearer. Anonymous clients can use GET auth/session and native
antiforgery cookie/header; listing and product detail remain public GETs. Price visibility permission is required.

A syntactically valid request returns 200 with isValid, code, pricing (nullable), warnings and requiresCartValidation.
Invalid selections or quantities under native business rules return isValid=false and no price. Invalid input IDs,
duplicates, unsupported controls in submitted choices return 400; inaccessible product 404, forbidden price 403.
Pricing contains currencyCode, unitPrice, regularUnitPrice, lineTotal, discountAmount and taxInclusive.
Native calculation includes explicit attribute selections and combination prices, including tier/role rules.
This is an isolated product estimate, not a stock reservation or cart total: existing cart quantities, required
products, cart access, shipping, order discounts and checkout conditions are revalidated during cart/checkout.
No cart write, new table, package, Core or OData modification. Variant validation input is shared with CartFacade.
Existing GET /price and /variants remain available.
Tests added for DTO/route/Swagger contracts; build and SQL-backed runtime quote tests are still required.
