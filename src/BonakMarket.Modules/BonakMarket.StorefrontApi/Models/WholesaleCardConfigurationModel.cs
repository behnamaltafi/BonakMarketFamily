using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using BonakMarket.Web.Modelling;

namespace BonakMarket.StorefrontApi.Models;

[LocalizedDisplay("Plugins.StorefrontApi.Fields.")]
public sealed class WholesaleCardConfigurationModel : ModelBase
{
    [LocalizedDisplay("*UnitsPerPackageSpecificationId")]
    [Range(0, int.MaxValue)]
    public int UnitsPerPackageSpecificationId { get; set; }

    [LocalizedDisplay("*PackageQuantityUnitId")]
    [Range(0, int.MaxValue)]
    public int PackageQuantityUnitId { get; set; }

    [BindNever]
    public IReadOnlyList<SelectListItem> PackageSpecifications { get; set; } = Array.Empty<SelectListItem>();

    [BindNever]
    public IReadOnlyList<SelectListItem> PackageQuantityUnits { get; set; } = Array.Empty<SelectListItem>();
}
