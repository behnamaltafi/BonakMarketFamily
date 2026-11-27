#nullable enable
using System.ComponentModel.DataAnnotations;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Models;

public sealed class CategorySearchRequest : PageRequest
{
    [StringLength(StorefrontLimits.MaxSearchLength)] public string? Search { get; set; }
    [Range(0, int.MaxValue)] public int? ParentId { get; set; }
    public bool? ShowOnHomePage { get; set; }
}
