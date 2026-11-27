#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed class CheckoutAddressesRequest
{
    [Range(1, int.MaxValue)] public int Revision { get; set; }
    [Range(1, int.MaxValue)] public int? BillingAddressId { get; set; }
    [Range(1, int.MaxValue)] public int? ShippingAddressId { get; set; }
}
