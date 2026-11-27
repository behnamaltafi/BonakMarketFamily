#nullable enable
using System.ComponentModel.DataAnnotations;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Models;

public sealed class BrandSearchRequest : PageRequest
{
    [StringLength(StorefrontLimits.MaxSearchLength)] public string? Search { get; set; }
}
