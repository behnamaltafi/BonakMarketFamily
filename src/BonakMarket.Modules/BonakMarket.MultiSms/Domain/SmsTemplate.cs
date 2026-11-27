using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Domain;

namespace BonakMarket.MultiSms.Domain;

[Table("MultiSmsTemplate")]
[Index(nameof(Key), IsUnique = true)]
public sealed class SmsTemplate : BaseEntity
{
    [Required, MaxLength(80)] public string Key { get; set; }
    [Required, MaxLength(160)] public string Title { get; set; }
    [Required, MaxLength(2000)] public string Parameters { get; set; }
    public bool IsOtp { get; set; }
    public bool IsActive { get; set; }
}

[Table("MultiSmsTemplateProvider")]
[Index(nameof(TemplateId), nameof(ProviderName), IsUnique = true)]
public sealed class SmsTemplateProvider : BaseEntity
{
    public int TemplateId { get; set; }
    [ForeignKey(nameof(TemplateId))] public SmsTemplate Template { get; set; }
    [Required, MaxLength(50)] public string ProviderName { get; set; }
    [Required, MaxLength(100)] public string ExternalTemplateId { get; set; }
    [Required, MaxLength(4000)] public string ParameterMapping { get; set; }
}
