#nullable enable
using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed class PrepareCheckoutReviewRequest
{
    [Range(1, int.MaxValue)] public int Revision { get; set; }
}
