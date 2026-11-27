using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Domain;

namespace BonakMarket.ERP.Domain;

[Table("ErpConnection")]
[Index(nameof(CompanyCode), nameof(Code), IsUnique = true, Name = "UX_ErpConnection_Company_Code")]
public sealed class ErpConnection : BaseEntity
{
    [Required, MaxLength(80)] public string CompanyCode { get; set; }
    [Required, MaxLength(80)] public string SourceCode { get; set; }
    [Required, MaxLength(80)] public string Code { get; set; }
    [Required, MaxLength(150)] public string Name { get; set; }
    [Required, MaxLength(80)] public string Provider { get; set; }
    [Required, MaxLength(500)] public string BaseUrl { get; set; }
    public bool Enabled { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    [Required, MaxLength(80)] public string AuthenticationType { get; set; }
    public string ProviderSettingsJson { get; set; }
    public string AuthenticationSettingsJson { get; set; }
    public string EncryptedAuthenticationSettings { get; set; }
    public bool LegacyLoginPayload { get; set; }
    public Guid ConfigurationVersion { get; set; } = Guid.NewGuid();
}
