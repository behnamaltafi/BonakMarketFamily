# Storefront catalog — web and mobile integration

All routes are under /api/storefront/v1. Guest access is subject to native catalog permissions. Send a valid Bearer access token for customer-specific pricing and cart capabilities; omit Authorization entirely for guests. An invalid token returns 401. Production requires HTTPS. Do not share-cache customer/store-specific responses.

## Products

- GET /products?search=rice&page=1&pageSize=20 — list/browse/search, paginated. Filters: categoryId, brandId, minPrice, maxPrice, inStock, hasDiscount, sort (Initial, NameAsc, NameDesc, PriceAsc, PriceDesc, Newest), includePrice. Reset page to 1 when filters change.
- GET /products/suggestions?search=rice&limit=5 — lightweight search-as-you-type, 250–350 ms debounce. Empty/short/disabled terms return 200 with empty items; no price or total count. Limit capped by administrator settings. Cancel superseded requests and ignore stale responses.
- GET /products/facets?search=rice — matchingProducts, category/brand counts, availability counts and nullable priceRange. Send the same filters as the list except page, pageSize, sort and includePrice. Counts are conjunctive (not disjunctive); clear a selected filter to see alternatives. Category counts use direct mappings. Lists can be truncated; use categories/brands for broader selection.
- GET /products/{id} — product details, images, specifications, variants, purchase rules and allowed actions.
- POST /products/{id}/quote — { quantity, variants: [{ variantAttributeId, valueId }] }. Calculate and validate the selected configuration without writing to the cart. Read isValid, code, pricing, warnings and requiresCartValidation even on HTTP 200. No stock reservation. Cart/checkout remain authoritative.

The list now includes availability (available/out_of_stock; sellable includes untracked stock or permitted backorders), minimumQuantity, maximumQuantity, quantityStep, allowedQuantities, quantityUnit (nullable), requiresConfiguration, canAddToCart, hasDiscount (nullable) and isPreOrder. A maximum below the minimum or zero means there is no supported purchase quantity in this API. Quantity is capped at 1000, as in cart and quote. canAddToCart is a capability for the current authenticated customer and minimum purchase; a true result is not a guarantee that a specific variant/quantity or existing cart is valid. When requiresConfiguration is true, use product details and quote; unsupported product types remain unsupported. Guest canAddToCart is false.

Price.finalPrice is an indicative **unit price**, calculated at price.pricingQuantity (minimum supported purchase quantity), not a line total. It may differ from quote or cart pricing. Price may be null for forbidden/unavailable price or includePrice=false. hasDiscount follows the native catalog discount flag; it is null without price-display permission. Actual discount amount is in price/quote. For a nonblank search, omitted inStock follows admin availability settings. Explicit true/false selects sellable/unavailable products; disabled variant combinations are excluded from sellability.

Price filters and range use base currency and active catalog/special prices, before tier/customer discounts, tax and variant adjustments. Price bounds are inclusive; products with call-for-price or customer-entered price are not included in bounds. Sorting alone keeps those products at the end. Price/discount filtering and price sorting require price-display permission. Facets priceRange is null without permission or eligible matching prices. priceFilterCurrencyCode describes the currency for bounds; displayed price has its own currencyCode.

All three search endpoints use the same scoped SQL matching and supported configured fields (name, SKU, short description). Simple Persian terms normalize Arabic ي/ك, ordinary spaces and ZWNJ on input and stored/current-language translated values; configured Contains/StartsWith/ExactMatch mode still applies. Other terms use native search parsing. No fuzzy search or external search index is promised. Explicit name sort remains based on stored product name. Search-as-you-type uses a smaller image and omits purchase/price calculations.

## Categories and brands

- GET /categories?parentId=0 — root categories. Positive parentId returns direct children. Omit parentId for a flat paginated list. Optional search and showOnHomePage.
- GET /categories/{id} — same item shape, 404 for missing/inaccessible category.
- GET /brands?search=...&page=1&pageSize=20 — paginated brand list in admin display order; ties by localized name and ID.
- GET /brands/{id} — same item shape, 404 for missing/inaccessible brand.

Category items include parentId, displayOrder, hasChildren and showOnHomePage. hasChildren is based on visible children, not the search or page. Hidden/unpublished/deleted ancestor branches and detached categories are excluded using the native tree path. Product category filters respect the visible branch and non-system category mappings. Names use current-language translation with stored-name fallback. All catalog image URLs are produced by native media services in page-sized batches; missing, deleted, hidden and transient images return null. Category/brand thumbnails use configured sizes. Resolve relative URLs against the API origin and render a client placeholder for null.

## Migration

Old /search, /search/instant and /search/facets routes remain removed. Use /products, /products/suggestions and /products/facets. This change additionally removes GET /products/{id}/variants (use detail.variants) and GET /products/{id}/price (use detail.price for the indicative unit price or POST /products/{id}/quote for quantity/variant validation and pricing). Update web/mobile clients before deployment. All other catalog contracts remain additive.

HTTP 400 validation, 401 invalid token, 403 permission, 404 missing/inaccessible entity, 503 unavailable store use the existing problem response contract. Do not retry invalid requests automatically. FullDescriptionHtml is HTML content; render through the client's safe HTML policy.
