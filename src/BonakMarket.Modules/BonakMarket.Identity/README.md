# BonakMarket Customer Identity

This in-process module owns native Customer sign-in/registration, verified phone binding, OTP challenges and quotas, profile, JWT sessions and MVC login widgets. It consumes only Core ISmsSender and ISmsDeliveryAvailability, with MultiSms installed as the configured transport. No new user entity or database is introduced.

Install MultiSms -> Identity -> StorefrontApi. Configure SMS transport in MultiSms and OTP login / primary login in Identity. In SMS Templates, map the seeded LOGINOTP logical template to approved provider templates. Identity selects independent logical keys for login/registration, phone enrollment/change, and reauthentication (all default to LOGINOTP). The login template must be routable when enabling SMS login or changing its key; other flows have independent readiness. Unchanged unavailable templates do not block policy edits. The UI defaults to a shared template unless different keys are configured. Existing issued OTP challenges can be validated during a delivery outage, subject to the explicit Identity enable switch and existing challenge security checks. The previous OTP message text setting is retired; message content is maintained in provider-approved templates. No Identity schema migration or OTP policy reset is required. Identity seeds the two global settings from the legacy SmsGatewaySettings on first install, without overriding existing Identity settings. Keep all existing Data Protection keys and MobileApi signing configuration.

The existing /sms-login and /api/sms-login routes are unchanged, including enrollment aliases, and the native password login remains available at /login?method=password. JWT issuer/audience and OTP protection purpose remain unchanged. The legacy MultiSmsLoginPhone, MultiSmsOtpChallenge, MultiSmsOtpQuota and MultiSmsMobileApiSession SQL tables and migration versions are preserved. Only their owning module changes. Do not reset or uninstall the old gateway to upgrade. Back up the database and verify fresh install and upgrade on staging. Clean stale output files before deployment.

See dev-docs/framework/web-api/storefront-api.md for account routes, token flow and deployment checks.

## Versioned customer API

| Controller | Method | Canonical path | Legacy alias |
| --- | --- | --- | --- |
| SmsLoginApi | POST | /api/identity/v1/auth/sms/request | /api/sms-login/request |
| SmsLoginApi | POST | /api/identity/v1/auth/sms/verify | /api/sms-login/verify |
| AuthApi | GET | /api/identity/v1/auth/session | /api/sms-login/session |
| AuthApi | POST | /api/identity/v1/auth/refresh | /api/sms-login/refresh |
| AuthApi | POST | /api/identity/v1/auth/logout | /api/sms-login/logout |
| ProfileApi | GET | /api/identity/v1/me/profile | /api/sms-login/profile |
| ProfileApi | PUT | /api/identity/v1/me/profile | /api/sms-login/profile |
| ProfileApi | GET | /api/identity/v1/me/addresses | /api/sms-login/profile/addresses |
| AccountSecurityApi | POST | /api/identity/v1/me/security/phone/request | /api/sms-login/profile/phone/request; /api/sms-login/enroll/request |
| AccountSecurityApi | POST | /api/identity/v1/me/security/phone/verify | /api/sms-login/profile/phone/verify; /api/sms-login/enroll/verify |

Legacy POST /api/sms-login/profile is retained. Use the canonical PUT for new clients. Profile writes retain verified-phone onboarding rules, and addresses remain read-only. No changes to JWT, migrations, database or native password login. Identity owns /docs/identity and /docs/identity/identity/swagger.json independently of StorefrontApi. /docs/customer lists enabled customer documents. Development enables documentation; use Identity__EnableSwagger=true outside Development (and restrict access at the proxy if needed). Legacy API routes remain operational but are excluded from OpenAPI. See the linked documentation for authentication and CSRF details.
