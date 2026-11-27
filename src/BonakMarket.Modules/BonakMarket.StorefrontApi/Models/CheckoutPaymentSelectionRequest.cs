#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed class CheckoutPaymentSelectionRequest
{
    [Range(1, int.MaxValue)] public int Revision { get; set; }
    [Required, StringLength(200)] public string SystemName { get; set; } = "";
}
