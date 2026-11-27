using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Catalog.Attributes;
using BonakMarket.Core.Common;
using BonakMarket.Core.Data;
using BonakMarket.Core.Security;
using BonakMarket.StorefrontApi.Configuration;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.Web.Controllers;
using BonakMarket.Web.Modelling.Settings;

namespace BonakMarket.StorefrontApi.Controllers;

// Native WebApi ApiControllerModelConvention opts in any controller with ApiExplorerSettings,
// even IgnoreApi=true. Keep this conventional MVC admin controller unannotated.
[Permission("configuration.setting")]
public sealed class StorefrontApiAdminController(SmartDbContext db) : AdminController
{
    [HttpGet, LoadSetting(false)]
    public async Task<IActionResult> Configure(WholesaleCardSettings settings, CancellationToken ct)
    {
        var model = new WholesaleCardConfigurationModel
        {
            UnitsPerPackageSpecificationId = settings.UnitsPerPackageSpecificationId,
            PackageQuantityUnitId = settings.PackageQuantityUnitId
        };
        await PopulateAsync(model, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, SaveSetting(false)]
    public async Task<IActionResult> Configure(WholesaleCardConfigurationModel model, WholesaleCardSettings settings, CancellationToken ct)
    {
        await PopulateAsync(model, ct);
        if (model.UnitsPerPackageSpecificationId > 0 &&
            !await db.Set<SpecificationAttribute>().AnyAsync(x => x.Id == model.UnitsPerPackageSpecificationId, ct))
            ModelState.AddModelError(nameof(model.UnitsPerPackageSpecificationId), T("Plugins.StorefrontApi.InvalidSpecification").Value);
        if (model.PackageQuantityUnitId > 0 &&
            !await db.Set<QuantityUnit>().AnyAsync(x => x.Id == model.PackageQuantityUnitId, ct))
            ModelState.AddModelError(nameof(model.PackageQuantityUnitId), T("Plugins.StorefrontApi.InvalidQuantityUnit").Value);
        if (model.PackageQuantityUnitId > 0 && model.UnitsPerPackageSpecificationId <= 0)
            ModelState.AddModelError(nameof(model.UnitsPerPackageSpecificationId), T("Plugins.StorefrontApi.PackageSpecificationRequired").Value);

        if (!ModelState.IsValid) return View(model);
        settings.UnitsPerPackageSpecificationId = model.UnitsPerPackageSpecificationId;
        settings.PackageQuantityUnitId = model.PackageQuantityUnitId;
        NotifySuccess(T("Admin.Common.DataSuccessfullySaved"));
        return RedirectToAction(nameof(Configure));
    }

    private async Task PopulateAsync(WholesaleCardConfigurationModel model, CancellationToken ct)
    {
        var specs = await db.Set<SpecificationAttribute>().AsNoTracking()
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        var units = await db.Set<QuantityUnit>().AsNoTracking()
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        model.PackageSpecifications = Options(specs.Select(x => (x.Id, x.Name)), model.UnitsPerPackageSpecificationId);
        model.PackageQuantityUnits = Options(units.Select(x => (x.Id, x.Name)), model.PackageQuantityUnitId);
    }

    private IReadOnlyList<SelectListItem> Options(IEnumerable<(int Id, string Name)> rows, int current)
    {
        var options = new List<SelectListItem> { new(T("Plugins.StorefrontApi.Disabled").Value, "0") };
        options.AddRange(rows.Select(x => new SelectListItem(x.Name + " (#" + x.Id + ")", x.Id.ToString())));
        if (current > 0 && !options.Any(x => x.Value == current.ToString()))
            options.Add(new SelectListItem(T("Plugins.StorefrontApi.Unavailable").Value + " (#" + current + ")", current.ToString()));
        return options;
    }
}
