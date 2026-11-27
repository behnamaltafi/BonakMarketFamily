using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record ShipmentDetailDto(ShipmentDto Shipment, List<ShipmentLineDto> Items);
