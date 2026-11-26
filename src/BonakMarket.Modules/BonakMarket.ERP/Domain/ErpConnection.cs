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
    [Required, MaxLength(80)] public string Code { get; set; }
    [Required, MaxLength(150)] public string Name { get; set; }
    [Required, MaxLength(80)] public string Provider { get; set; }
    [Required, MaxLength(500)] public string BaseUrl { get; set; }
    public bool Enabled { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    [MaxLength(200)] public string ODataPrefix { get; set; } = "odata/";
    [Required, MaxLength(200)] public string LoginPath { get; set; } = "api/auth/login";
    [MaxLength(40)] public string AuthorizationScheme { get; set; } = "Bearer";
    public bool UseLogin { get; set; } = true;
    public string EncryptedLoginBody { get; set; }
}
