#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed class CheckoutShippingSelectionRequest
{
    [Range(1, int.MaxValue)] public int Revision { get; set; }
    [Range(0, int.MaxValue)] public int ShippingMethodId { get; set; }
    [Required, StringLength(200)] public string Provider { get; set; } = "";
    // The observed provider rate is a precondition, not a client-supplied price.
    [Required] public decimal? ObservedRate { get; set; }
}
