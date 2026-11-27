using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record ShipmentDto(int Id, int OrderId, string TrackingNumber, string TrackingUrl,
    DateTime? ShippedDateUtc, DateTime? DeliveryDateUtc);
