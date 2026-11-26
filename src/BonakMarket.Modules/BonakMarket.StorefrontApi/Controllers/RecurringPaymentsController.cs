using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Checkout.Payment;
using BonakMarket.Core.Data;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1/recurring-payments")]
[StorefrontPermission(StorefrontPermissions.Orders)]
public sealed class RecurringPaymentsController(SmartDbContext db, StorefrontRequestContext request,
    IOrderProcessingService processing) : StorefrontController
{
    private IQueryable<RecurringPayment> Owned() => db.RecurringPayments.Where(x => !x.Deleted &&
        StorefrontScope.Orders(db, request.Customer, request.StoreId).Any(o => o.Id == x.InitialOrderId));

    [HttpGet]
    public async Task<ActionResult<PageDto<RecurringPaymentDto>>> List([FromQuery] PageRequest page, CancellationToken ct) =>
        Ok(await Owned().AsNoTracking().OrderByDescending(x => x.CreatedOnUtc).ThenByDescending(x => x.Id)
            .Select(x => new RecurringPaymentDto(x.Id, x.InitialOrderId, x.CycleLength, x.CyclePeriodId,
                x.TotalCycles, x.StartDateUtc, x.IsActive)).PageAsync(page, ct));

    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        var payment = await Owned().Include(x => x.InitialOrder).ThenInclude(x => x.Customer).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (payment == null) return Error(404, "recurring_payment_not_found");
        if (!payment.IsActive) return NoContent();
        if (!await processing.CanCancelRecurringPaymentAsync(payment, request.Customer))
            return Error(403, "recurring_payment_cancellation_not_allowed");
        await processing.CancelRecurringPaymentAsync(payment);
        return NoContent();
    }
}
