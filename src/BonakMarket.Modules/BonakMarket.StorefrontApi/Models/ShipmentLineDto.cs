using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record ShipmentLineDto(int OrderItemId, int Quantity);
