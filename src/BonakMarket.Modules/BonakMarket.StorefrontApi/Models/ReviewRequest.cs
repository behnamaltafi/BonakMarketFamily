using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed class ReviewRequest
{
    [Required, StringLength(200)] public string Title { get; set; }
    [Required, StringLength(10000)] public string ReviewText { get; set; }
    [Range(1, 5)] public int Rating { get; set; }
    public bool GdprConsent { get; set; }
}
