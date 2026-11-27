using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using BonakMarket.Web.Modelling;

namespace BonakMarket.StorefrontApi.Models;

public sealed class WholesaleCardConfigurationModel : ModelBase
{
    [Range(0, int.MaxValue)]
    public int UnitsPerPackageSpecificationId { get; set; }

    [Range(0, int.MaxValue)]
    public int ConsumerPriceSpecificationId { get; set; }

    [StringLength(3)]
    public string ConsumerPriceCurrencyCode { get; set; }

    [BindNever]
    public IReadOnlyList<SelectListItem> PackageSpecifications { get; set; } = Array.Empty<SelectListItem>();

    [BindNever]
    public IReadOnlyList<SelectListItem> ConsumerPriceSpecifications { get; set; } = Array.Empty<SelectListItem>();
}
