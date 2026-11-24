# Storefront experience and customer services
Base /api/storefront/v1. GET /bootstrap returns store/logo, current language/culture/direction, working and
base currency, store configuration feature flags. Flags are not permission grants. Cart quantities and private
account information are separate: GET /me/summary (authenticated) returns counts, points and guild requirement.
Null count means unavailable/not permitted, not zero. Rewards use the existing customer-wide balance contract.
GET /me/reviews returns owned reviews including moderation state for products visible in the current store.
Native reviews do not have a store id. Existing returns, shipments, downloads, recurring payments, reviews,
stock subscriptions and reward endpoints remain available.

GET /pages and GET /pages/{id} expose published non-widget, non-password-protected native CMS topics only,
with customer ACL and store restriction checks. No password is returned. Localized fields follow native
working language. Intro/body are HTML: render with a sanitized renderer, never execute arbitrary scripts.
This is not a renderer for MVC widgets or shopping themes. Media links can be relative; resolve against
trusted storefront origin. All endpoints retain the native no-store cache policy; no shared cache is introduced.
GET /products/facets accepts the same filters as GET /products, ignores page and returns matchingProducts,
direct visible category/brand counts (up to 50 each, with truncation flags). It is not disjunctive faceting and
does not expose specification facets. Price-based filters retain DisplayPrice authorization.
Native checkout confirmation flag is false by default and enabled only by explicit configuration on SQL Server;
see offline-checkout-confirmation.md. Native online payment remains false.
