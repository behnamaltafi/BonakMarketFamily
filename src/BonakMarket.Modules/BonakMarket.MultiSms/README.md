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

## SMS.ir configuration

Select SmsIr as primary or safe fallback in SMS Gateway. Enable it and set the encrypted API key. Set the numeric dedicated line number for free-text messages and the existing manual Test SMS action. For OTP, optionally enter an approved SMS.ir Verify template ID and its exact code parameter name (default Code). When the template is configured, OTP uses /v1/send/verify with the original six-digit code (not parsed from message text); the message body is controlled by the approved provider template. Without a template ID, OTP uses /v1/send/bulk and the Identity message template. A Verify-only configuration does not support free-text tests or general SMS. Verify requires an approved SMS.ir template and appropriate account permissions. Test real delivery with your own account after deployment. No raw responses or OTPs are logged. No automatic retry on ambiguous delivery.

## Optional OTP code history

ShowOtpCodeInHistory defaults to false and requires HistoryEnabled. Only the explicit six-digit VerificationCode is captured (not parsed from text), using a dedicated time-limited Data Protection purpose and the existing Body column. The admin details view rechecks the setting on every request. When enabled, all environments reveal the code immediately, including Production. This exposes active authentication codes to authorized administrators; use only for troubleshooting. The encrypted value becomes unreadable after 15 minutes and is removed with normal history retention. Disabling hides existing codes immediately and stops new capture; re-enabling within the window may show previously captured codes. This is an opt-in diagnostic feature; keep disabled in normal production. No raw OTP body or test text is captured by this option.
