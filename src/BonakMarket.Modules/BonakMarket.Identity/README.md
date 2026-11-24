# BonakMarket Customer Identity

This in-process module owns native Customer sign-in/registration, verified phone binding, OTP challenges and quotas, profile, JWT sessions and MVC login widgets. It consumes only Core ISmsSender and ISmsDeliveryAvailability, with MultiSms installed as the configured transport. No new user entity or database is introduced.

Install MultiSms -> Identity -> StorefrontApi. Configure SMS transport in MultiSms and OTP login / primary login in Identity. Identity seeds the two global settings from the legacy SmsGatewaySettings on first install, without overriding existing Identity settings. Keep all existing Data Protection keys and MobileApi signing configuration.

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

Legacy POST /api/sms-login/profile is retained. Use the canonical PUT for new clients. Profile writes retain verified-phone onboarding rules, and addresses remain read-only. No changes to JWT, migrations, database or native password login. Swagger is available through the Storefront API module. See the linked documentation for authentication and CSRF details.
