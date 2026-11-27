#nullable restore
using System.ComponentModel.DataAnnotations;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Models;

public class PageRequest
{
    [Range(1, StorefrontLimits.MaxPage)] public int Page { get; set; } = 1;
    [Range(1, StorefrontLimits.MaxPageSize)] public int PageSize { get; set; } = 20;
}
