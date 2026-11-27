using System.ComponentModel.DataAnnotations;
using BonakMarket.ERP.Domain;

namespace BonakMarket.ERP.Models;

public sealed class ConnectionModel
{
    public int Id { get; set; }
    public Guid ConfigurationVersion { get; set; }
    [Required, MaxLength(80)] public string CompanyCode { get; set; }
    [Required, MaxLength(80)] public string SourceCode { get; set; }
    [Required, MaxLength(80)] public string Code { get; set; }
    [Required, MaxLength(150)] public string Name { get; set; }
    [Required, MaxLength(80)] public string Provider { get; set; } = "Varanegar";
    [Required, MaxLength(500)] public string BaseUrl { get; set; }
    public bool Enabled { get; set; }
    [Range(1, 120)] public int TimeoutSeconds { get; set; } = 30;
    [Required, MaxLength(80)] public string AuthenticationType { get; set; } = "VaranegarLogin";
    [MaxLength(16000)] public string ProviderSettingsJson { get; set; }
    [MaxLength(8000)] public string AuthenticationSettingsJson { get; set; }
    [MaxLength(400)] public string NewUsername { get; set; }
    [MaxLength(2000)] public string NewPassword { get; set; }
    public bool ClearCredentials { get; set; }
    public bool HasCredentials { get; set; }
    public bool LegacyLoginPayload { get; set; }
    public string[] Providers { get; set; } = [];
    public string[] AuthenticationTypes { get; set; } = [];
}
public sealed record ErpIndexModel(IReadOnlyList<ErpConnectionListItem> Connections, IReadOnlyList<ErpSyncRun> Runs);
public sealed record ErpConnectionListItem(int Id, string CompanyCode, string Code, string Name, string Provider, bool Enabled, string SourceCode);
public sealed record CatalogModel(int ConnectionId, int Skip, IReadOnlyList<ErpCatalogProduct> Rows, IReadOnlyList<ProductTemplateOption> Templates);
public sealed record ProductTemplateOption(int Id, string Name);
