#nullable restore
using System.ComponentModel.DataAnnotations;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Models;

public sealed class CouponRequest
{
    [Required, StringLength(200)] public string Code { get; set; }
}
