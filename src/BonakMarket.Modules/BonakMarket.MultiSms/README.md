# BonakMarket MultiSms — SMS transport

This module owns Kavenegar, Melipayamak, SMS.ir and development Mock transports, runtime provider selection, safe failover, encrypted credentials, message history and administrator delivery testing. It implements Core ISmsSender and ISmsDeliveryAvailability. Customer authentication, OTP generation/verification, JWT, profile and phone enrollment are owned by BonakMarket.Identity. Install MultiSms before Identity; configure login switches on Identity's own configuration page.

## SQL Server message and attempt history

The central `SmsGatewaySender` records a parent `MultiSmsMessage` row
and a `MultiSmsSendAttempt` row for each transport attempt. Safe failover
creates **two attempt rows** under the same message: e.g.
Kavenegar/Rejected → Melipayamak/Accepted.

- `Accepted` means provider acceptance **only**, not handset delivery.
- `Rejected` means a definite provider rejection; `Unknown` means a
  potentially delivered request failed without a definitive receipt.
  Ambiguous sends are NEVER automatically retried.
- The default `HistoryEnabled=true` and `HistoryRetentionDays=90`
  may be changed in Admin settings (1–3650 days).
  Expired records are removed in bounded batches during subsequent history
  writes (at most once every six hours per running process); this is
  **best-effort request-triggered cleanup**, not a guaranteed exact-time
  scheduler. A disabled/idle gateway will not run retention cleanup.
- Full recipient phone numbers are retained in SQL to permit exact-match
  administrator search; admin history views mask them. Restrict database access
  accordingly. No message body is stored by default.
- The optional `StoreNonOtpMessageBody` flag stores bodies of messages
  explicitly classified `General`. OTP and manual Test message bodies are
  never persisted even if enabled. Optional OTP code diagnostics are separate from body storage.
- History write errors are non-blocking: they cannot turn an accepted SMS into
  a retryable error. The admin status is operational history, not a delivery
  status verification service. There is no delivery callback integration yet.
- Additional migration `20261119102000_SmsMessageHistory.cs` creates the
  two module-owned history tables (FluentMigrator), without touching Customer
  or core BonakMarket schema.

The history is at Admin → Settings → SMS History or from the SMS module's
configuration page. The history listing includes a filter by full destination
phone, message type, status, pagination and drill-down into attempts.


## Development testing
Run in Development, configure and enable Mock in MultiSms and enable OTP in Identity. The administrative development inbox is temporary, process-local, and unavailable in production. /api/sms-gateway/test remains protected by native administrator permissions and antiforgery. No anonymous delivery endpoint is exposed.

Preserve existing SQL history and Data Protection keys during upgrade. See ../BonakMarket.Identity/README.md for account API and migration instructions.

## Delivery modes and upgrade

Routes are now FreeText and Template, independent of the message purpose (General, Otp, Test).
Old settings must be re-entered; there is no implicit legacy routing. The module migration creates MultiSmsTemplate and MultiSmsTemplateProvider, seeds LOGINOTP and adds nullable DeliveryMode and TemplateKey columns to message history. Existing history remains intact.

Configure provider credentials and capability switches in SMS Gateway. Use SMS Templates to define a logical key, title, purpose and parameter schema, then map the logical parameters to an approved external ID for each provider. Keys are case-insensitive and stored uppercase. Identity configures independent logical keys for login/registration, enrollment/change and reauthentication. All default to LOGINOTP; every selected template requires Code and a usable mapping in the configured Template route. OTP cannot use FreeText. Define further logical keys when adding business notifications.

Example OrderShipped: logical parameters OrderNumber, TrackingCode; Kavenegar slots token, token2; Melipayamak slots 0, 1; SMS.ir slots ORDER, TRACKING. A mapping missing on a provider permits moving to the next configured template provider only when safe failover is enabled. No cross-mode fallback. No raw template values are persisted.

Template callers use ISmsSender.SendAsync with SmsSendRequest(phone, null) { DeliveryMode = SmsDeliveryMode.Template, TemplateKey = "OrderShipped", TemplateParameters = new Dictionary<string, string> { ... } }. Existing free-text callers remain unchanged.

Melipayamak supports Console token (simple/shared) and Legacy REST (SendSMS/BaseServiceNumber). Console receives ordered args; legacy joins the ordered values with semicolon and rejects delimiter-containing values. Verify with real accounts before rollout.

Admin tests use saved settings and exactly the selected provider. Template tests can be run from the template edit page; OTP test does not create a login challenge or persist test codes. Keep a non-SMS administrator login available until configuration is verified. Mock is isolated, Development only. Accepted is not handset delivery.

## Optional OTP code history

ShowOtpCodeInHistory defaults to false and requires HistoryEnabled. Only the explicit six-digit VerificationCode is captured (not parsed from text), using a dedicated time-limited Data Protection purpose and the existing Body column. The admin details view rechecks the setting on every request. When enabled, all environments reveal the code immediately, including Production. This exposes active authentication codes to authorized administrators; use only for troubleshooting. The encrypted value becomes unreadable after 15 minutes and is removed with normal history retention. Disabling hides existing codes immediately and stops new capture; re-enabling within the window may show previously captured codes. This is an opt-in diagnostic feature; keep disabled in normal production. No raw OTP body or test text is captured by this option.

## Unified readiness and Test Center
Provider capability, route readiness, and actual delivery tests are separate. All local checks use SmsReadinessService, including credential decryption, route membership and template mapping. Templates show the reason for each unusable route provider. Test Center sends through configured routing or directly to one provider; it always records a Test message and does not expose OTP values. Direct testing can bypass the global gateway switch, but route testing cannot. Test results are provider acceptance only, not delivery confirmation. OTP mapping for Melipayamak and Kavenegar is automatic; other mappings are per logical parameter. No new schema migration is required for these UI and readiness changes.
