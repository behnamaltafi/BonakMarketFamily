# Customer API foundation — mobile contract

Scope: canonical /api/identity/v1 and /api/storefront/v1 MVC actions and invalid customer bearer responses.
The shared customer result filter is registered by Identity (a required dependency of StorefrontApi).
No Core, native storefront MVC, OData, or database migration changes.

## Errors
Canonical MVC errors return application/problem+json, with status, stable code, title and traceId.
Existing validation errors and native warnings are retained. Existing storefront snake_case titles are preserved;
read code rather than interpreting title/detail. An example:
```json
{"status":422,"title":"cart_validation_failed","code":"cart_validation_failed","traceId":"...","warnings":["..."]}
```
Successful responses and legacy /api/sms-login aliases keep their previous bodies.
The filter handles MVC action and short-circuit results; unhandled exceptions, unmatched routes and reverse-proxy
errors are outside this contract until their runtime behavior is verified. Do not retry mutations automatically.
On a 401 from an access-token request, coordinate refresh once; on invalid_refresh_token return to login.

## Pagination
Paged canonical responses have items, page, pageSize, totalCount, totalPages, hasNextPage, hasPreviousPage.
Page starts at 1; maximum page is 10000, page size 1–100. Address default remains 50; Storefront default remains 20.
Reference lookup lists (e.g. active guilds, address settings) are intentionally not paged.

## Validation
Run CustomerApiFoundationTests, SwaggerContractTests, StorefrontContractTests and CustomerOnboardingContractTests.
Verify runtime error media types and response schema, authorization/CSRF short-circuit, aliases, and OData isolation.
No build or test execution is implied by the source commit.
