# Native Smartstore SMS Gateway and OTP login

This module is developed on a **fresh branch from Smartstore main**. It uses
Smartstore's existing ASP.NET Core Identity `Customer` and
`SignInManager<Customer>`. It does **not** add an `AppUser` entity, replace
Customer authentication, or depend on earlier identity-separation branches.

## Included

- A standalone `ISmsSender` contract in Smartstore.Core; extend with
  additional `ISmsProvider` implementations without changing OTP logic.
- Kavenegar and Melipayamak senders, runtime primary selection and safe
  failover only after definite rejection. Timeouts/unknown outcomes are not
  automatically retried to avoid duplicate SMS.
- Smartstore Admin module configuration: enable/disable SMS, choose primary
  and fallback providers, rotate encrypted credentials, send a test SMS,
  and independently enable/disable OTP login.
- Native Customer OTP login via `/sms-login`, authenticated phone enrollment
  via `/sms-login/enroll`. Original password, external and checkout login flows
  are preserved.
- Encrypted-at-rest one-time codes, 5 minute lifetime, at most five
  verification attempts per challenge, 2 minute resend cooldown and
  10 sends in the challenge's rolling 24-hour window. Authenticated
  enrollment additionally has a cross-phone per-account limit.
- Migration: `Migrations/20261118140000_Initial.cs` creates three
  **module-owned** tables through Smartstore FluentMigrator:
  `MultiSmsLoginPhone`, `MultiSmsOtpChallenge` and `MultiSmsOtpQuota`.
  It does not modify `Customer`, `Order`, or default Identity tables.

## Install and configure

1. Build `Smartstore.sln` and install **Multi-provider SMS Gateway** under
   **Admin → Plugins/Modules**.
2. Open its Configure page. Configure Kavenegar and/or Melipayamak,
   enable **SMS Gateway**, and use **Send test SMS** before enabling OTP.
3. Enable **SMS one-time-code login**. Settings take effect for subsequent
   requests without a process restart.
4. **Sign in with SMS** appears automatically on the existing password login
   screen when both SMS Gateway and SMS one-time-code login are enabled.
   Separate CMS widget activation is not required. Alternatively, navigate
   to `/sms-login` directly.
5. Sign in **with your existing password first**. Open
   `/sms-login/enroll` and verify a number you control. Only one verified
   mobile number can be assigned to one Customer.
6. Log out. Enter the verified mobile and the received OTP at
   `/sms-login`. Invalid/unknown numbers receive the same generic response.
7. Back up the SQL Server database before installing/upgrading on a live shop.
   Verify the schema and restore procedure on a staging clone first.

## Security and deployment

- Enrollment is explicitly tied to an authenticated, active, registered
  Customer. A phone from `Customer.GenericAttributes.Phone` is **never**
  considered a verified login identity.
- Native Smartstore cookies and Identity manager are reused. Customers with
  a separate two-factor-authentication requirement must continue using their
  existing sign-in method; this OTP route refuses to bypass that requirement.
- Challenge state is stored in the shop database (shared across nodes).
  Concurrent updates use optimistic concurrency. Unique indexes stop
  phone and challenge duplicates.
- Codes and provider secrets use ASP.NET Core Data Protection. Persist and
  share the Data Protection key ring across all application nodes; protect and
  back up the keys. Losing the ring invalidates pending OTPs and stored
  provider credentials.
- Do not log OTP bodies, Data Protection secrets, or provider URLs containing
  API credentials. A provider receipt confirms acceptance, **not delivery**.
- The built-in OTP throttles are per-number. Apply **global/per-IP limits at
  your reverse proxy or WAF** to stop SMS pumping across many phone numbers.
- Confirm all storefront registration approval rules, captcha strategy,
  checkout and Identity permissions in staging. Real provider sending cannot
  be tested without valid SMS provider credentials and account balance.

## Primary login method

In module configuration, **Use SMS as the primary sign-in method** controls
whether the native `/login` page opens SMS sign-in. It defaults to enabled and
only applies when both the SMS Gateway and SMS login are enabled. Turn it off
to make the password form primary while keeping the SMS option available.
Changes take effect on the next request without restarting the application.

The password form remains reachable at `/login?method=password`, including for
administrators and users who have not enrolled a phone yet. Native password
POST handling is preserved. Enrollment sends unauthenticated users to this
password form and returns them to phone verification after login. Local return
URLs and guest checkout are preserved; external return URLs are discarded.

## JSON API

These endpoints are available independently of the optional OData WebApi module.
They use native Customer cookies, not JWTs or administrative OData credentials.

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/api/sms-login/session` | Availability, session, verified phone and CSRF token |
| POST | `/api/sms-login/request` | Request login OTP: `{ "phone": "09123456789" }` |
| POST | `/api/sms-login/verify` | Sign in: `{ "phone": "09123456789", "code": "123456" }` |
| POST | `/api/sms-login/enroll/request` | Request phone verification for the signed-in account |
| POST | `/api/sms-login/enroll/verify` | Confirm phone verification for the signed-in account |
| POST | `/api/sms-gateway/test` | Admin test: `{ "phone": "09123456789", "text": "Test" }` |

Keep cookies between requests. Read `requestToken` and `headerName` from Session,
then include that header on every POST, including anonymous login requests.
Fetch a fresh Session token after login or switching accounts. Missing/invalid
CSRF tokens return HTTP 400. Enrollment without a native login returns 401.
OTP-disabled endpoints return 404. Invalid codes return 400 with
`{ "code": "invalid_code" }`. Code requests return the same
`{ "code": "code_requested" }` for known and unknown numbers; they do not confirm
delivery. Existing cooldowns, quotas and attempt limits apply.

The delivery test additionally requires both `system.accessbackend` and
`configuration.setting` permissions. Success returns `accepted`, provider and
message ID; provider failures return HTTP 502 with `sms_not_confirmed`, without
credentials, exception details or message contents.

Example from a same-origin browser application (prepend the shop's path base
when hosted in a subdirectory):

```javascript
const session = await fetch('/api/sms-login/session', {
    credentials: 'same-origin'
}).then(response => response.json());

const response = await fetch('/api/sms-login/request', {
    method: 'POST',
    credentials: 'same-origin',
    headers: {
        'Content-Type': 'application/json',
        [session.headerName]: session.requestToken
    },
    body: JSON.stringify({ phone: '09123456789' })
});
```

The OTP views explicitly use the host `_Layout` to load the active theme and
generated assets. They display one step at a time and support resending a code
or changing the phone number without JavaScript.

## Verify in development

```powershell
dotnet build Smartstore.sln -c Release
dotnet test test/Smartstore.Core.Tests/Smartstore.Core.Tests.csproj -c Release --filter "FullyQualifiedName~SmsGatewaySenderTests|FullyQualifiedName~SmsPhoneNormalizerTests|FullyQualifiedName~SmsApiControllerTests|FullyQualifiedName~SmsLoginWidgetSourceTests|FullyQualifiedName~SmsPrimaryLoginFilterTests"
```

Do not run EF Core `Add-Migration` for these module tables. Smartstore's
native FluentMigrator migration is the schema owner for these objects, and
migration history is recorded in `__MigrationVersionInfo`.
