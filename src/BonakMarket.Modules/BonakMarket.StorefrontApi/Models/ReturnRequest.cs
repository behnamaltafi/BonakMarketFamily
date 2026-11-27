using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed class ReturnRequest
{
    [Required, StringLength(4000)] public string Reason { get; set; }
    [Required, StringLength(4000)] public string Action { get; set; }
    [StringLength(4000)] public string Comments { get; set; }
    [Required, MinLength(1), MaxLength(100)] public List<ReturnQuantity> Items { get; set; }
}
