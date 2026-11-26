# Native mobile authentication

New additive endpoints (existing web SMS routes, aliases and cookie flows are unchanged):

1. POST /api/identity/v1/auth/mobile/sms/request with JSON {"phone":"09121234567"}.
2. Response: {"challengeId":"opaque","expiresIn":300,"resendAfter":120,"maskedPhone":"0912***4567"}.
3. POST /api/identity/v1/auth/mobile/sms/verify with JSON {"challengeId":"opaque","code":"012345"}.
4. Response: accessToken, refreshToken, tokenType, expiresIn, profileRequired, missingRequiredFields.
5. Authorization: Bearer <accessToken> for protected APIs. Rotate using POST /api/identity/v1/auth/refresh;
   do not send an expired access token, refresh concurrently or retry a consumed refresh credential.
6. POST /api/identity/v1/auth/logout revokes the current mobile session.

HTTPS is required by default; application/json is mandatory. For isolated development/testing only, set `Identity:RequireHttps` to `false` (`Identity__RequireHttps=false` as an environment variable). The Development configuration opts out; all other environments inherit the secure default unless explicitly overridden. This setting applies to mobile SMS request/verify, optional legacy token issuance, refresh, customer bearer authentication, account operations and Storefront API authorization. Never disable HTTPS for real customer traffic; OTPs and credentials would travel unencrypted. Behind a trusted TLS-terminating reverse proxy, configure forwarded HTTPS headers instead. No cookie, CSRF preflight session request or issueTokens flag is needed.
Token-only sign-in never explicitly creates a native login cookie and does not invoke the browser guest-cart
sign-in flow. GET auth/session with bearer on the canonical route omits CSRF token values.
The old auth/sms/request and verify still require the native antiforgery flow.

The challenge ID is an opaque, time-limited protected ticket referencing the current OTP send generation.
Verification binds the ticket to that generation and consumes the existing SQL challenge atomically.
No new challenge table, no separate quota, and no change to the OTP provider contract.
Resending uses the existing shared per-phone cooldown (120 seconds), daily quota and five-attempt cap.
expiresIn and resendAfter are policy durations for UI guidance, not a guarantee that a new SMS was sent or
that an earlier challenge has that much time remaining. Suppressed/failed delivery and ineligible accounts
receive the same public response shape. Re-request after expiry; verification failures return invalid_code.
Use a persisted shared Data Protection key ring for multi-instance deployment. Never log OTP, challenge or tokens.
Verify upstream abuse controls for the public SMS endpoint (existing per-phone quota does not cap total distinct numbers).

MobileApi settings, token rotation, native registration rules, guild onboarding and profile endpoints are unchanged.
Session listing and logout-all are optional later enhancements. Test on SQL Server with OTP providers:
web/mobile shared quotas, resend invalidation, concurrent verification, replay, disabled accounts, invalid phone,
disabled SMS/token issuance, no browser login cookie, token refresh/logout, HTTPS and legacy cookie CSRF.
Test source only; no runtime execution claimed.
