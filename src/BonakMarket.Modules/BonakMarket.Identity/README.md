# BonakMarket Customer Identity

Identity owns native Customer sign-in/registration, verified phone binding, OTP challenges and quotas, profile, JWT sessions and the MVC account widgets. MultiSms remains the transport through Core ISmsSender and ISmsDeliveryAvailability.

Install MultiSms -> Identity -> StorefrontApi. Keep all existing Data Protection keys, MobileApi signing configuration, historical SQL tables and migrations. No new database or migration is introduced.

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
