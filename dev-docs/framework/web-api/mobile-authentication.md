# Unified customer authentication (web and mobile)

One public login contract, no native authentication cookie or Session API:

1. POST /api/identity/v1/auth/sms/request with JSON {"phone":"09121234567"}.
2. Response: {"challengeId":"opaque","expiresIn":300,"resendAfter":120,"maskedPhone":"0912***4567"} (durations are configuration dependent).
3. POST /api/identity/v1/auth/sms/verify with JSON {"challengeId":"opaque","code":"012345"}.
4. Response: accessToken, refreshToken, tokenType, expiresIn, profileRequired, missingRequiredFields.
5. GET /api/identity/v1/me/profile to read current profile and onboarding state with Authorization: Bearer <accessToken>. Use the same Bearer on protected Storefront routes; omit Authorization entirely for anonymous catalog.
6. POST /api/identity/v1/auth/refresh with JSON {"refreshToken":"..."} and no Authorization header. Replace both credentials atomically; use single-flight rotation, no parallel refresh or retry of a consumed token.
7. POST /api/identity/v1/auth/logout with valid Bearer and no body, OR omit Authorization and submit {"refreshToken":"..."} to revoke even after access expiry. Never send both. Expect 204; clear local credentials and authenticated caches. Logout does not remove a native MVC cookie or delete the customer cart/checkout draft.

Access lifetime 600 seconds; refresh session absolute lifetime 30 days. SQL validates/revokes the session; token signatures and issuance configuration are unchanged. The API never accepts ambient MVC login cookies for authentication. Visitor/session cookies used by the native host are not customer authentication credentials. Use HTTPS; development may explicitly disable Identity:RequireHttps only in isolated testing. For separate web origins configure Identity__AllowedOrigins__0=https://app.example.com; send fetch credentials:"omit", explicit Authorization and application/json; never put integration secrets in clients. Configure TLS forwarded headers on trusted proxies.

OTP challengeId is a protected ticket bound to the current send generation. Existing per-phone quotas and SQL challenge consumption are shared with native MVC login. Keep the Data Protection key ring and signing key across deployments. Honor Retry-After/retryAfter on rate-limited requests. A request success does not disclose whether a number exists or whether delivery occurred. Use the returned challengeId even on resend. Do not log credentials or OTP. The token-only login does not migrate the native guest cart or sign a browser in. The front must handle onboarding through profile and guild endpoints.

## Migration and release

Removed routes on this branch: /api/identity/v1/auth/mobile/sms/request, /verify, /auth/session, and /api/sms-login/* (legacy aliases). The canonical SMS verify accepts challengeId + code, no issueTokens flag. No change to native MVC /sms-login; the address editor and phone widget now use /sms-login/account/* with cookie/CSRF and shared services. No change to OData/ERP. Checkout browser links, invoices and payment retry still require a separately signed-in MVC browser; no automatic browser handoff is implemented.

Before merging, confirm all external consumers migrated. Build and test against SQL Server with OTP providers: new/existing customers, quotas, invalid/replayed OTP, cross-customer cookie/JWT isolation, refresh races, logout via both credentials, phone-change revocation, MVC address/phone CSRF, CORS preflight and OData regression.
