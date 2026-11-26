#nullable enable
using System.ComponentModel.DataAnnotations;
namespace BonakMarket.MultiSms.Models;
public sealed class SmsTestRequest
{
    [Required, StringLength(20)]
    public string Phone { get; set; } = string.Empty;
    [Required, StringLength(1500)]
    public string Text { get; set; } = string.Empty;
}
