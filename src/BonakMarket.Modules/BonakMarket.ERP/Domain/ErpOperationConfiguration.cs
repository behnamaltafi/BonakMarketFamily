using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Domain;

namespace BonakMarket.ERP.Domain;

[Table("ErpOperationConfiguration")]
[Index(nameof(CompanyCode), nameof(Operation), IsUnique = true, Name = "UX_ErpOperation_Company_Operation")]
public sealed class ErpOperationConfiguration : BaseEntity
{
    [Required, MaxLength(80)] public string CompanyCode { get; set; }
    [Required, MaxLength(40)] public string Operation { get; set; }
    public int ConnectionId { get; set; }
    public bool Enabled { get; set; }
    public bool ScheduleEnabled { get; set; }
    public int IntervalMinutes { get; set; } = 60;
    public int BatchSize { get; set; } = 100;
    public int NextOffset { get; set; }
    public DateTime? NextRunUtc { get; set; }
    public DateTime? LastSucceededUtc { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
}
