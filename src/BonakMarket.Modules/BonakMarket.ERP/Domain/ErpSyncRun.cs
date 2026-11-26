using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Domain;

namespace BonakMarket.ERP.Domain;
[Table("ErpSyncRun")]
[Index(nameof(ConnectionId), nameof(StartedUtc), Name = "IX_ErpSyncRun_Connection")]
public sealed class ErpSyncRun : BaseEntity
{
    public int ConnectionId { get; set; }
    [Required, MaxLength(40)] public string Operation { get; set; }
    [Required, MaxLength(30)] public string Status { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime? FinishedUtc { get; set; }
    public int Rows { get; set; }
    [MaxLength(100)] public string ErrorCode { get; set; }
}
