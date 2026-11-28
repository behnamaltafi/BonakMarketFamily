using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using BonakMarket.Admin.Models.Common;
using BonakMarket.ComponentModel;
using BonakMarket.Core.Localization;
using BonakMarket.Core.Rules.Filters;
using BonakMarket.Core.Security;
using BonakMarket.Core.Stores;
using BonakMarket.Web.Models.DataGrid;

namespace BonakMarket.Admin.Controllers;

public class CountryController : AdminController
{
    private readonly SmartDbContext _db;
    private readonly ILocalizedEntityService _localizedEntityService;
    private readonly IStoreMappingService _storeMappingService;

    public CountryController(
        SmartDbContext db,
        ILocalizedEntityService localizedEntityService,
        IStoreMappingService storeMappingService)
    {
        _db = db;
        _localizedEntityService = localizedEntityService;
        _storeMappingService = storeMappingService;
    }

    #region Utilities 

    private async Task UpdateLocalesAsync(Country country, CountryModel model)
    {
        foreach (var localized in model.Locales)
        {
            await _localizedEntityService.ApplyLocalizedValueAsync(country, x => x.Name, localized.Name, localized.LanguageId);
        }
    }

    private async Task UpdateLocalesAsync(StateProvince stateProvince, StateProvinceModel model)
    {
        foreach (var localized in model.Locales)
        {
            await _localizedEntityService.ApplyLocalizedValueAsync(stateProvince, x => x.Name, localized.Name, localized.LanguageId);
        }
    }

    private async Task PrepareCountryModelAsync(CountryModel model, Country country)
    {
        Guard.NotNull(model, nameof(model));

        if (country != null)
        {
            model.SelectedStoreIds = await _storeMappingService.GetAuthorizedStoreIdsAsync(country);
            model.NumberOfStates = country.StateProvinces.Count;
        }

        var currencies = await _db.Currencies
            .AsNoTracking()
            .ApplyStandardFilter(false)
            .ToListAsync();

        ViewBag.Currencies = currencies
            .Select(x => new SelectListItem { Text = x.GetLocalized(y => y.Name), Value = x.Id.ToString() })
            .ToList();
    }

    #endregion

    #region Countries

    public IActionResult Index()
    {
        return RedirectToAction(nameof(List));
    }

    [Permission(Permissions.Configuration.Country.Read)]
    public IActionResult List()
    {
        return View();
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Country.Read)]
    public async Task<IActionResult> CountryList(GridCommand command, CountrySearchListModel model)
    {
        var query = _db.Countries
            .AsNoTracking();

        if (model.SearchName.HasValue())
        {
            query = query.ApplySearchFilterFor(x => x.Name, model.SearchName);
        }

        if (model.SearchTwoLetterIsoCode.HasValue())
        {
            query = query.Where(x => x.TwoLetterIsoCode == model.SearchTwoLetterIsoCode);
        }

        if (model.SearchAllowsBilling.HasValue)
        {
            query = query.Where(x => x.AllowsBilling == model.SearchAllowsBilling);
        }

        if (model.SearchAllowsShipping.HasValue)
        {
            query = query.Where(x => x.AllowsShipping == model.SearchAllowsShipping);
        }

        if (model.SearchSubjectToVat.HasValue)
        {
            query = query.Where(x => x.SubjectToVat == model.SearchSubjectToVat);
        }

        if (model.SearchPublished.HasValue)
        {
            query = query.Where(x => x.Published == model.SearchPublished);
        }

        var countries = await query
            .OrderBy(x => x.DisplayOrder)
            .ApplyGridCommand(command)
            .ToPagedList(command)
            .LoadAsync();

        var mapper = MapperFactory.GetMapper<Country, CountryModel>();
        var countryModels = await countries
            .SelectAwait(async x =>
            {
                var model = await mapper.MapAsync(x);
                model.EditUrl = Url.Action(nameof(Edit), "Country", new { id = x.Id });

                return model;
            })
            .ToListAsync();

        var gridModel = new GridModel<CountryModel>
        {
            Rows = countryModels,
            Total = await countries.GetTotalCountAsync()
        };

        return Json(gridModel);
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Country.Update)]
    public async Task<IActionResult> CountryUpdate(CountryModel model)
    {
        var success = false;
        var country = await _db.Countries.FindByIdAsync(model.Id);

        if (country != null)
        {
            await MapperFactory.MapAsync(model, country);
            await _db.SaveChangesAsync();
            success = true;
        }

        return Json(new { success });
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Country.Delete)]
    public async Task<IActionResult> Delete(int id)
    {
        var country = await _db.Countries.FindByIdAsync(id);
        if (country == null)
        {
            return RedirectToAction(nameof(List));
        }

        // Don't delete associated countries.
        if (!await _db.Addresses.AnyAsync(x => x.CountryId == id) &&
            !await _db.Cities.AnyAsync(x => x.StateProvince.CountryId == id))
        {
            _db.Countries.Remove(country);
            await _db.SaveChangesAsync();

            NotifySuccess(T("Admin.Configuration.Countries.Deleted"));
            return RedirectToAction(nameof(List));
        }
        else
        {
            NotifyError(T("Admin.Configuration.Countries.CannotDeleteDueToAssociatedAddresses"));
            return RedirectToAction(nameof(Edit), new { id = id });
        }
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Country.Delete)]
    public async Task<IActionResult> CountryDelete(GridSelection selection)
    {
        var success = false;
        var numDeleted = 0;
        var ids = selection.GetEntityIds();

        if (ids.Any())
        {
            var countries = await _db.Countries.GetManyAsync(ids, true);
            var triedToDeleteAssociated = false;

            foreach (var country in countries)
            {
                if (await _db.Addresses.AnyAsync(x => x.CountryId == country.Id) ||
                    await _db.Cities.AnyAsync(x => x.StateProvince.CountryId == country.Id))
                {
                    triedToDeleteAssociated = true;
                    NotifyError(T("Admin.Configuration.Countries.CannotDeleteDueToAssociatedAddresses"));
                }
                else
                {
                    _db.Countries.Remove(country);
                }
            }

            numDeleted = await _db.SaveChangesAsync();

            if (triedToDeleteAssociated && numDeleted == 0)
            {
                success = false;
            }
            else
            {
                success = true;
            }
        }

        return Json(new { Success = success, Count = numDeleted });
    }

    [Permission(Permissions.Configuration.Country.Create)]
    public async Task<IActionResult> Create()
    {
        var model = new CountryModel();

        AddLocales(model.Locales);
        await PrepareCountryModelAsync(model, null);

        return View(model);
    }

    [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
    [Permission(Permissions.Configuration.Country.Create)]
    public async Task<IActionResult> Create(CountryModel model, bool continueEditing)
    {
        if (ModelState.IsValid)
        {
            var country = await MapperFactory.MapAsync<CountryModel, Country>(model);
            _db.Countries.Add(country);
            await _db.SaveChangesAsync();

            await UpdateLocalesAsync(country, model);
            await _storeMappingService.ApplyStoreMappingsAsync(country, model.SelectedStoreIds);
            await _db.SaveChangesAsync();

            await Services.EventPublisher.PublishAsync(new ModelBoundEvent(model, country, this));
            NotifySuccess(T("Admin.Configuration.Countries.Added"));

            return continueEditing
                ? RedirectToAction(nameof(Edit), new { id = country.Id })
                : RedirectToAction(nameof(List));
        }

        await PrepareCountryModelAsync(model, null);

        return View(model);
    }

    [Permission(Permissions.Configuration.Country.Read)]
    public async Task<IActionResult> Edit(int id)
    {
        var country = await _db.Countries
            .Include(x => x.StateProvinces)
            .FindByIdAsync(id, false);

        if (country == null)
        {
            return NotFound();
        }

        var model = await MapperFactory.MapAsync<Country, CountryModel>(country);

        AddLocales(model.Locales, (locale, languageId) =>
        {
            locale.Name = country.GetLocalized(x => x.Name, languageId, false, false);
        });

        await PrepareCountryModelAsync(model, country);

        return View(model);
    }

    [HttpPost, ParameterBasedOnFormName("save-continue", "continueEditing")]
    [Permission(Permissions.Configuration.Country.Update)]
    public async Task<IActionResult> Edit(CountryModel model, bool continueEditing)
    {
        var country = await _db.Countries
            .Include(x => x.StateProvinces)
            .FindByIdAsync(model.Id);

        if (country == null)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            await MapperFactory.MapAsync(model, country);
            await UpdateLocalesAsync(country, model);
            await _storeMappingService.ApplyStoreMappingsAsync(country, model.SelectedStoreIds);
            await _db.SaveChangesAsync();

            await Services.EventPublisher.PublishAsync(new ModelBoundEvent(model, country, this));
            NotifySuccess(T("Admin.Configuration.Countries.Updated"));

            return continueEditing
                ? RedirectToAction(nameof(Edit), new { id = country.Id })
                : RedirectToAction(nameof(List));
        }

        await PrepareCountryModelAsync(model, country);

        return View(model);
    }

    #endregion

    #region States / provinces

    [HttpPost]
    [Permission(Permissions.Configuration.Country.Read)]
    public async Task<IActionResult> States(int countryId, GridCommand command)
    {
        var stateProvinces = await _db.StateProvinces
            .Where(x => x.CountryId == countryId)
            .OrderBy(x => x.DisplayOrder)
            .ApplyGridCommand(command)
            .ToPagedList(command)
            .LoadAsync();

        var mapper = MapperFactory.GetMapper<StateProvince, StateProvinceModel>();
        var stateProvinceModels = await stateProvinces
            .SelectAwait(async x => await mapper.MapAsync(x))
            .ToListAsync();

        var gridModel = new GridModel<StateProvinceModel>
        {
            Rows = stateProvinceModels,
            Total = await stateProvinces.GetTotalCountAsync()
        };

        return Json(gridModel);
    }

    [Permission(Permissions.Configuration.Country.Update)]
    public IActionResult StateCreatePopup(int countryId, string btnId, string formId)
    {
        var model = new StateProvinceModel
        {
            CountryId = countryId
        };

        AddLocales(model.Locales);

        ViewBag.BtnId = btnId;
        ViewBag.FormId = formId;

        return View(model);
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Country.Update)]
    public async Task<IActionResult> StateCreatePopup(StateProvinceModel model, string btnId, string formId)
    {
        var country = await _db.Countries.FindByIdAsync(model.CountryId);
        if (country == null)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var stateProvince = await MapperFactory.MapAsync<StateProvinceModel, StateProvince>(model);
            _db.StateProvinces.Add(stateProvince);
            await _db.SaveChangesAsync();

            await UpdateLocalesAsync(stateProvince, model);

            ViewBag.RefreshPage = true;
            ViewBag.BtnId = btnId;
            ViewBag.FormId = formId;
        }

        return View(model);
    }

    [Permission(Permissions.Configuration.Country.Read)]
    public async Task<IActionResult> StateEditPopup(int id, string btnId, string formId)
    {
        var stateProvince = await _db.StateProvinces.FindByIdAsync(id);
        if (stateProvince == null)
        {
            return NotFound();
        }

        var model = await MapperFactory.MapAsync<StateProvince, StateProvinceModel>(stateProvince);

        AddLocales(model.Locales, (locale, languageId) =>
        {
            locale.Name = stateProvince.GetLocalized(x => x.Name, languageId, false, false);
        });

        ViewBag.BtnId = btnId;
        ViewBag.FormId = formId;

        return View(model);
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Country.Update)]
    public async Task<IActionResult> StateEditPopup(StateProvinceModel model, string btnId, string formId)
    {
        var stateProvince = await _db.StateProvinces.FindByIdAsync(model.Id);
        if (stateProvince == null)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            await MapperFactory.MapAsync(model, stateProvince);
            await UpdateLocalesAsync(stateProvince, model);
            await _db.SaveChangesAsync();

            ViewBag.RefreshPage = true;
            ViewBag.BtnId = btnId;
            ViewBag.FormId = formId;
        }

        return View(model);
    }

    [HttpPost]
    [Permission(Permissions.Configuration.Country.Update)]
    public async Task<IActionResult> StateUpdate(StateProvinceModel model)
    {
        var success = false;
        var stateProvinces = await _db.StateProvinces.FindByIdAsync(model.Id);

        if (stateProvinces != null)
        {
            await MapperFactory.MapAsync(model, stateProvinces);
            await _db.SaveChangesAsync();
            success = true;
        }

        return Json(new { success });
    }

    [Permission(Permissions.Configuration.Country.Update)]
    public async Task<IActionResult> StateDelete(GridSelection selection)
    {
        var success = false;
        var numDeleted = 0;
        var ids = selection.GetEntityIds();

        if (ids.Any())
        {
            var stateProvinces = await _db.StateProvinces.GetManyAsync(ids, true);

            foreach (var province in stateProvinces)
            {
                if (await _db.Cities.AnyAsync(c => c.StateProvinceId == province.Id) ||
                    await _db.Addresses.AnyAsync(a => a.StateProvinceId == province.Id))
                {
                    NotifyError(T("Admin.Configuration.Countries.Cities.ProvinceInUse"));
                    continue;
                }
                _db.StateProvinces.Remove(province);
            }

            numDeleted = await _db.SaveChangesAsync();
            success = numDeleted > 0;
        }

        return Json(new { Success = success, Count = numDeleted });
    }

    [HttpGet, Permission(Permissions.Configuration.Country.Read)]
    public async Task<IActionResult> Cities(int countryId, int? provinceId, CancellationToken ct)
    {
        var country = await _db.Countries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == countryId, ct);
        if (country == null) return NotFound();
        var provinces = await _db.StateProvinces.AsNoTracking().Where(x => x.CountryId == countryId)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ToListAsync(ct);
        var province = provinceId.HasValue ? provinces.FirstOrDefault(x => x.Id == provinceId) : provinces.FirstOrDefault();
        if (provinceId.HasValue && province == null) return NotFound();
        ViewBag.CountryId = countryId;
        ViewBag.CountryName = country.Name;
        ViewBag.Provinces = provinces;
        ViewBag.ProvinceId = province?.Id;
        ViewBag.CanUpdate = await Services.Permissions.AuthorizeAsync(Permissions.Configuration.Country.Update);
        var cities = province == null ? new List<City>() : await _db.Cities.AsNoTracking()
            .Where(x => x.StateProvinceId == province.Id).OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).ToListAsync(ct);
        return View(cities);
    }

    [HttpPost, ValidateAntiForgeryToken, Permission(Permissions.Configuration.Country.Update)]
    public async Task<IActionResult> CitySave(int countryId, int provinceId, int id, string name, bool published,
        int displayOrder, CancellationToken ct)
    {
        if (!await _db.StateProvinces.AnyAsync(p => p.Id == provinceId && p.CountryId == countryId, ct))
            return NotFound();
        name = name?.Trim();
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (string.IsNullOrEmpty(name) || name.Length > 100 || displayOrder < 0 ||
            await _db.Cities.AnyAsync(c => c.StateProvinceId == provinceId && c.Id != id && c.Name == name, ct))
        {
            NotifyError(T("Admin.Configuration.Countries.Cities.Invalid"));
            return RedirectToAction(nameof(Cities), new { countryId, provinceId });
        }
        var city = id == 0 ? new City { StateProvinceId = provinceId } :
            await _db.Cities.SingleOrDefaultAsync(c => c.Id == id && c.StateProvinceId == provinceId, ct);
        if (city == null) return NotFound();
        city.Name = name;
        city.Published = published;
        city.DisplayOrder = displayOrder;
        if (id == 0) _db.Cities.Add(city);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return RedirectToAction(nameof(Cities), new { countryId, provinceId });
    }

    [HttpPost, ValidateAntiForgeryToken, Permission(Permissions.Configuration.Country.Update)]
    public async Task<IActionResult> CityDelete(int countryId, int provinceId, int id, CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var city = await _db.Cities.SingleOrDefaultAsync(c => c.Id == id && c.StateProvinceId == provinceId &&
            c.StateProvince.CountryId == countryId, ct);
        if (city == null) return NotFound();
        if (await _db.Addresses.IgnoreQueryFilters().AnyAsync(a => a.CityId == id, ct))
            NotifyError(T("Admin.Configuration.Countries.Cities.InUse"));
        else
        {
            _db.Cities.Remove(city);
            await _db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return RedirectToAction(nameof(Cities), new { countryId, provinceId });
    }

    /// <summary>
    /// Permission validation is not required here.
    /// This action method gets called via an AJAX request.
    /// </summary>
    public async Task<IActionResult> GetStatesByCountryId(string countryId, bool? addEmptyStateIfRequired, bool? addAsterisk)
    {
        var country = await _db.Countries.FindByIdAsync(countryId.ToInt());
        var states = new List<StateProvince>();
        if (country != null)
        {
            states = await _db.StateProvinces
                .AsNoTracking()
                .Where(x => x.CountryId == country.Id)
                .ToListAsync();
        }

        var result = (from s in states select new { id = s.Id, name = s.Name }).ToList();

        if (addEmptyStateIfRequired.HasValue && addEmptyStateIfRequired.Value && result.Count == 0)
        {
            result.Insert(0, new { id = 0, name = T("Admin.Address.OtherNonUS").Value });
        }

        if (addAsterisk.HasValue && addAsterisk.Value)
        {
            result.Insert(0, new { id = 0, name = "*" });
        }

        return new JsonResult(result);
    }

    #endregion
}