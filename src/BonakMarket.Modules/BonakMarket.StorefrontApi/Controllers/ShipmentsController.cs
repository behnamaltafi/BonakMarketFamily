using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;
using BonakMarket.Core.Checkout.Shipping;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1")]
[StorefrontPermission(StorefrontPermissions.Orders)]
public sealed class ShipmentsController(SmartDbContext db, StorefrontRequestContext request) : StorefrontController
{
    private IQueryable<Shipment> Owned() => db.Shipments.AsNoTracking().Where(x =>
        StorefrontScope.Orders(db, request.Customer, request.StoreId).Any(o => o.Id == x.OrderId));

    private static IQueryable<ShipmentDto> Project(IQueryable<Shipment> query) => query.Select(x =>
        new ShipmentDto(x.Id, x.OrderId, x.TrackingNumber, x.TrackingUrl, x.ShippedDateUtc, x.DeliveryDateUtc));

    [HttpGet("orders/{orderId:int}/shipments")]
    public async Task<ActionResult<List<ShipmentDto>>> List(int orderId, CancellationToken ct)
    {
        if (!await StorefrontScope.Orders(db, request.Customer, request.StoreId).AnyAsync(x => x.Id == orderId, ct))
            return Error(404, "order_not_found");
        return Ok(await Project(Owned().Where(x => x.OrderId == orderId).OrderBy(x => x.Id)).ToListAsync(ct));
    }

    [HttpGet("shipments/{id:int}")]
    public async Task<ActionResult<ShipmentDetailDto>> Get(int id, CancellationToken ct)
    {
        var shipment = await Project(Owned().Where(x => x.Id == id)).FirstOrDefaultAsync(ct);
        if (shipment == null) return Error(404, "shipment_not_found");
        var lines = await db.Set<ShipmentItem>().AsNoTracking().Where(x => x.ShipmentId == id)
            .Select(x => new ShipmentLineDto(x.OrderItemId, x.Quantity)).ToListAsync(ct);
        return Ok(new ShipmentDetailDto(shipment, lines));
    }
}
