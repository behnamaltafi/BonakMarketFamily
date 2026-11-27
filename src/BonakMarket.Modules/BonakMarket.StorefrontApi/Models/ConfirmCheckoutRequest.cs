#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed class ConfirmCheckoutRequest
{
    [Range(1, int.MaxValue)] public int Revision { get; set; }
    [Required] public decimal? ExpectedOrderTotal { get; set; }
    [Required, StringLength(5)] public string CurrencyCode { get; set; } = "";
    [Required, StringLength(64, MinimumLength = 64)] public string CartFingerprint { get; set; } = "";
    public bool AcceptTerms { get; set; }
}
