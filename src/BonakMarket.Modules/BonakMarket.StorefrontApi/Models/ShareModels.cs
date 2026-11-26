using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed class ShareRequest
{
    [Required, EmailAddress, StringLength(254)] public string FriendEmail { get; set; }
    [StringLength(1000)] public string PersonalMessage { get; set; }
    public bool GdprConsent { get; set; }
}
