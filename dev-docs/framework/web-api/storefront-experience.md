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
Native checkout confirmation and online payment flags are false pending the final payment integration.
