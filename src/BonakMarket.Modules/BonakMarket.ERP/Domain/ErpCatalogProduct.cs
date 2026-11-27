using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Domain;

namespace BonakMarket.ERP.Domain;

// A staged ERP product is not a saleable product. The company, stable source key and external ID
// form the canonical identity; only one ERP product can own a local product.
[Table("ErpCatalogProduct")]
[Index(nameof(CompanyCode), nameof(SourceCode), nameof(ExternalId), IsUnique = true, Name = "UX_ErpCatalogProduct_External")]
[Index(nameof(ProductId), IsUnique = true, Name = "UX_ErpCatalogProduct_Local")]
public sealed class ErpCatalogProduct : BaseEntity
{
    [Required, MaxLength(80)] public string CompanyCode { get; set; }
    [Required, MaxLength(80)] public string SourceCode { get; set; }
    [Required, MaxLength(150)] public string ExternalId { get; set; }
    public int ConnectionId { get; set; }
    public int? ProductId { get; set; }
    [Required, MaxLength(400)] public string Sku { get; set; }
    [Required, MaxLength(400)] public string Name { get; set; }
    public bool ActiveForSale { get; set; }
    public string SnapshotJson { get; set; }
    public DateTime LastSeenUtc { get; set; }
}
