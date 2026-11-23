using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Checkout.Tax;
using BonakMarket.Core.Data;
using BonakMarket.Core.Security;
using BonakMarket.Web.Controllers;
using BonakMarket.Web.Models.DataGrid;

namespace BonakMarket.Tax.Controllers;

public class TaxFixedRateController : AdminController
{
    private readonly SmartDbContext _db;
    private readonly ITaxService _taxService;

    public TaxFixedRateController(SmartDbContext db, ITaxService taxService)
    {
        _db = db;
        _taxService = taxService;
    }

    public IActionResult Index()
    {
        return RedirectToAction(nameof(Configure));
    }

    public async Task<IActionResult> Configure()
    {
        if (!await _db.TaxCategories.AnyAsync())
        {
            NotifyWarning(T("Plugins.Tax.CountryStateZip.NoTaxCategoriesFound"));
        }

        ViewBag.Provider = _taxService.LoadTaxProviderBySystemName("Tax.FixedRate").Metadata;

        return View();
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Tax.Read)]
    public async Task<IActionResult> List(GridCommand command)
    {
        var taxRates = await _db.TaxCategories
            .AsNoTracking()
            .OrderBy(x => x.DisplayOrder)
            .ToPagedList(command)
            .LoadAsync();

        var taxRateModels = await taxRates.SelectAwait(async x => new FixedTaxRateModel
        {
            TaxCategoryId = x.Id,
            TaxCategoryName = x.Name,
            Rate = await Services.Settings.GetSettingByKeyAsync<decimal>($"Tax.TaxProvider.FixedRate.TaxCategoryId{x.Id}")
        })
            .ToListAsync();

        var gridModel = new GridModel<FixedTaxRateModel>
        {
            Rows = taxRateModels,
            Total = await taxRates.GetTotalCountAsync()
        };

        return Json(gridModel);
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Tax.Update)]
    public async Task<IActionResult> Update(FixedTaxRateModel model)
    {
        await Services.Settings.ApplySettingAsync($"Tax.TaxProvider.FixedRate.TaxCategoryId{model.TaxCategoryId}", model.Rate);
        return Json(new { success = true });
    }
}