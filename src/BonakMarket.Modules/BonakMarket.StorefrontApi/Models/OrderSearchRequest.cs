#nullable restore
using System.ComponentModel.DataAnnotations;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Models;

public sealed class OrderSearchRequest : PageRequest
{
    public int? OrderStatusId { get; set; }
    public int? PaymentStatusId { get; set; }
    public int? ShippingStatusId { get; set; }
}
