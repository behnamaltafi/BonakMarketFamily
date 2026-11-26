# Native order placement safety

Core changes approved for the Storefront API checkout integration:
- OrderGuid has a unique database index across all orders, including soft-deleted ones.
- The migration creates the new unique index before removing the old nonunique index.
- A supplied already-used OrderGuid is rejected before provider calls with order_already_exists.
- Optional ProcessPaymentRequest.ExpectedOrderTotal is the exact native primary-currency amount accepted by the customer.
  Recompute the final native total with request caching disabled for the final total calculation when supplied.
  Compare after ApplyPricingData and before ProcessPayment (including PreProcessPayment).
  A mismatch rolls back and returns order_total_changed. Null preserves old callers.
  The accepted value never overwrites the amount calculated by the server.

## SQL Server preflight (read-only)
```sql
SELECT OrderGuid, COUNT_BIG(*) AS DuplicateCount
FROM dbo.[Order]
GROUP BY OrderGuid
HAVING COUNT_BIG(*) > 1;
```
Run on a backup/staging copy before deployment, then on production in the maintenance window.
If duplicates exist, stop and investigate original payment/order references with the business owner.
Do not delete orders or regenerate existing GUIDs automatically. The unique index creation itself
enforces the check even if the preflight was skipped. Down restores the old nonunique index.

## Boundaries
These changes do not implement provider idempotency or guarantee exactly-once external payment.
Concurrent requests can both pass the preliminary GUID check before either commits, and
native payment processing can happen before the database insertion. A caller still needs a durable
attempt reservation, provider-specific idempotency and reconciliation for uncertain results.
No checkout confirm or payment attempt endpoint is enabled in this commit.
No OData, Web, or Storefront API routes are changed. Core unit/metadata tests were added; run
SQL Server migration, duplicate insert, concurrency, provider-not-called-on-mismatch, legacy browser,
recurring and recovery integration tests before deployment.
