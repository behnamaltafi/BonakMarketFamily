# BonakMarket Customer Identity

Identity owns native Customer sign-in/registration, verified phone binding, OTP challenges and quotas, profile, JWT sessions and the MVC account widgets. MultiSms remains the transport through Core ISmsSender and ISmsDeliveryAvailability.

Install MultiSms -> Identity -> StorefrontApi. Keep all existing Data Protection keys, MobileApi signing configuration, historical SQL tables and migrations. No new database or migration is introduced.

Configure SMS transport in MultiSms and OTP/primary login in Identity. In SMS Templates, map the seeded LOGINOTP logical template to approved provider templates. Identity selects independent logical keys for login/registration, phone enrollment/change, and reauthentication (all default to LOGINOTP). The login template must be routable when enabling SMS login or changing its key; other flows have independent readiness. Unchanged unavailable templates do not block policy edits. Existing OTP challenges can be verified during delivery outages, subject to the Identity enable switch and challenge security checks. Message text is managed in provider-approved templates; the old OTP message text setting is retired. No Identity schema migration or OTP policy reset is required.

## Customer API (JWT only)

| Method | Path | Authentication |
| --- | --- | --- |
| POST | /api/identity/v1/auth/sms/request | Anonymous |
| POST | /api/identity/v1/auth/sms/verify | Anonymous |
| POST | /api/identity/v1/auth/refresh | Refresh token in JSON |
| POST | /api/identity/v1/auth/logout | Bearer or current refresh token in JSON |
| GET/PUT | /api/identity/v1/me/profile | Bearer |
| GET/POST/PUT/DELETE | /api/identity/v1/me/addresses | Bearer |
| POST | /api/identity/v1/me/security/phone/* | Bearer |
| GET | /api/identity/v1/guilds | Anonymous |

See dev-docs/framework/web-api/mobile-authentication.md for the unified request/response contract and migration instructions. The API no longer accepts native authentication cookies, issueTokens or CSRF headers. Old /api/sms-login/*, /auth/mobile/sms/* and /auth/session routes are removed on this feature branch; coordinate consumers before deploying.

The original MVC /sms-login and /login?method=password remain. MVC address and phone widgets now use private same-origin /sms-login/account/* endpoints with the existing cookie and antiforgery token rendered in the view; they share the same address and OTP services. Native admin, MVC login/logout and OData are unaffected. A token-only API login does not establish a browser session.

Identity owns /docs/identity and /docs/identity/identity/swagger.json. /docs/customer lists enabled customer documents. Enable Identity__EnableSwagger=true outside Development only with suitable network restrictions. For a separate browser origin, configure exact Identity:AllowedOrigins entries (no wildcard and no credentialed CORS).

### Address geography

- Anonymous GET /api/identity/v1/locations/provinces returns published Iranian provinces with published cities.
- Anonymous GET /api/identity/v1/locations/provinces/{id}/cities returns published cities belonging to that province.
- Address POST/PUT accepts cityId (legacy city name resolves within the selected province), latitude and longitude. Country remains server-selected Iran. Coordinates are optional: omit both on PUT to preserve, send both null to clear, or send both numbers to set. Only pair/range checks apply; no geographic boundary validation. Existing addresses remain readable. For a new form send cityId rather than city; the city string remains an optional legacy input and an output snapshot.
- List/detail and order address snapshots return cityId, latitude and longitude. No geocoding or map provider dependency.
