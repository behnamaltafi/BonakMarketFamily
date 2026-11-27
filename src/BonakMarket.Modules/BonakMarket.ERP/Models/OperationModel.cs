using System.ComponentModel.DataAnnotations;
using BonakMarket.ERP.Domain;

namespace BonakMarket.ERP.Models;
public sealed class OperationModel
{
    [Required, MaxLength(80)] public string CompanyCode { get; set; }
    [Required, MaxLength(40)] public string Operation { get; set; } = "Products";
    public int ConnectionId { get; set; }
    public bool Enabled { get; set; }
    public bool ScheduleEnabled { get; set; }
    [Range(1, 10080)] public int IntervalMinutes { get; set; } = 60;
    [Range(1, 100)] public int BatchSize { get; set; } = 100;
}
public sealed record OperationsModel(IReadOnlyList<ErpOperationConfiguration> Routes, IReadOnlyList<ErpConnectionListItem> Connections, string[] Operations);
