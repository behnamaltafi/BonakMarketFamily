using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Catalog.Attributes;
using BonakMarket.Core.Data;
using BonakMarket.Core.Security;
using BonakMarket.StorefrontApi.Configuration;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Services;
using BonakMarket.Web.Controllers;
using BonakMarket.Web.Modelling.Settings;

namespace BonakMarket.StorefrontApi.Controllers;

[Permission("configuration.setting")]
public sealed class StorefrontApiAdminController(SmartDbContext db) : AdminController
{
    [HttpGet, LoadSetting(false)]
    public async Task<IActionResult> Configure(WholesaleCardSettings settings, CancellationToken ct)
    {
        var model = new WholesaleCardConfigurationModel
        {
            UnitsPerPackageSpecificationId = settings.UnitsPerPackageSpecificationId,
            ConsumerPriceSpecificationId = settings.ConsumerPriceSpecificationId,
            ConsumerPriceCurrencyCode = settings.ConsumerPriceCurrencyCode
        };
        await PopulateAsync(model, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, SaveSetting(false)]
    public async Task<IActionResult> Configure(WholesaleCardConfigurationModel model, WholesaleCardSettings settings, CancellationToken ct)
    {
        var specs = await PopulateAsync(model, ct);
        if (model.UnitsPerPackageSpecificationId > 0 && !specs.Any(x => x.Id == model.UnitsPerPackageSpecificationId))
            ModelState.AddModelError(nameof(model.UnitsPerPackageSpecificationId), "Select an existing product specification.");

        if (model.ConsumerPriceSpecificationId > 0)
        {
            if (!specs.Any(x => x.Id == model.ConsumerPriceSpecificationId && IsPrivatePriceSpecification(x)))
                ModelState.AddModelError(nameof(model.ConsumerPriceSpecificationId), "Select an existing specification that is hidden, nonessential and not filterable.");
            if (model.ConsumerPriceSpecificationId == model.UnitsPerPackageSpecificationId)
                ModelState.AddModelError(nameof(model.ConsumerPriceSpecificationId), "Package size and consumer price must use different specifications.");
            if (WholesaleCardMetadata.CurrencyCode(model.ConsumerPriceCurrencyCode) == null)
                ModelState.AddModelError(nameof(model.ConsumerPriceCurrencyCode), "Enter an explicit three-letter currency code, e.g. IRR or IRT.");
        }
        else if (!string.IsNullOrWhiteSpace(model.ConsumerPriceCurrencyCode) &&
            WholesaleCardMetadata.CurrencyCode(model.ConsumerPriceCurrencyCode) == null)
        {
            ModelState.AddModelError(nameof(model.ConsumerPriceCurrencyCode), "Enter a three-letter currency code or leave it empty.");
        }

        if (!ModelState.IsValid) return View(model);
        settings.UnitsPerPackageSpecificationId = model.UnitsPerPackageSpecificationId;
        settings.ConsumerPriceSpecificationId = model.ConsumerPriceSpecificationId;
        settings.ConsumerPriceCurrencyCode = WholesaleCardMetadata.CurrencyCode(model.ConsumerPriceCurrencyCode) ?? string.Empty;
        NotifySuccess(T("Admin.Common.DataSuccessfullySaved"));
        return RedirectToAction(nameof(Configure));
    }

    private static bool IsPrivatePriceSpecification(SpecificationAttribute spec) =>
        !spec.ShowOnProductPage && !spec.Essential && !spec.AllowFiltering;

    private async Task<List<SpecificationAttribute>> PopulateAsync(WholesaleCardConfigurationModel model, CancellationToken ct)
    {
        var specs = await db.Set<SpecificationAttribute>().AsNoTracking()
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(ct);
        model.PackageSpecifications = Options(specs, model.UnitsPerPackageSpecificationId);
        model.ConsumerPriceSpecifications = Options(specs.Where(IsPrivatePriceSpecification), model.ConsumerPriceSpecificationId);
        return specs;
    }

    private static IReadOnlyList<SelectListItem> Options(IEnumerable<SpecificationAttribute> specs, int current)
    {
        var options = new List<SelectListItem> { new("Disabled", "0") };
        options.AddRange(specs.Select(x => new SelectListItem(x.Name + " (#" + x.Id + ")", x.Id.ToString())));
        if (current > 0 && !options.Any(x => x.Value == current.ToString()))
            options.Add(new SelectListItem("Unavailable / invalid specification (#" + current + ")", current.ToString()));
        return options;
    }
}
