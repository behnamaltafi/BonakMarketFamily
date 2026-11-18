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
4. Under **Admin → CMS → Widgets**, activate the new MultiSms widget to
   show **Sign in with SMS** on Smartstore's existing password login screen.
   Alternatively, navigate to `/sms-login` directly.
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

## Verify in development

```powershell
dotnet build Smartstore.sln -c Release
dotnet test test/Smartstore.Core.Tests/Smartstore.Core.Tests.csproj -c Release --filter "FullyQualifiedName~SmsGatewaySenderTests|FullyQualifiedName~SmsPhoneNormalizerTests"
```

Do not run EF Core `Add-Migration` for these module tables. Smartstore's
native FluentMigrator migration is the schema owner for these objects, and
migration history is recorded in `__MigrationVersionInfo`.
