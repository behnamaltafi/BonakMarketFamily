using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed class ReturnQuantity
{
    [Range(1, int.MaxValue)] public int OrderItemId { get; set; }
    [Range(1, int.MaxValue)] public int Quantity { get; set; }
}
