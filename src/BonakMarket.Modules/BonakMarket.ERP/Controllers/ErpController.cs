using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Security;
using BonakMarket.ERP.Domain;
using BonakMarket.ERP.Models;
using BonakMarket.ERP.Authentication;
using BonakMarket.ERP.Contracts;
using BonakMarket.ERP.Infrastructure;
using BonakMarket.ERP.Services;
using BonakMarket.Web.Controllers;
using BonakMarket.Scheduling;
using BonakMarket.ERP.Tasks;

namespace BonakMarket.ERP.Controllers;

[Permission("configuration.setting")]
public sealed class ErpController(SmartDbContext db, ErpProviderResolver resolver, ErpSecrets secrets, ErpCatalogService catalog, ErpDraftProductService drafts, ErpAuthenticationResolver authentication, ErpEndpointPolicy endpoints, ErpOperationRouter router, ErpOperationRunner runner, ITaskStore tasks, ErpOperationLock operationLock) : AdminController
{
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(new ErpIndexModel(await db.Set<ErpConnection>().AsNoTracking().OrderBy(x => x.CompanyCode)
            .Select(x => new ErpConnectionListItem(x.Id, x.CompanyCode, x.Code, x.Name, x.Provider, x.Enabled, x.SourceCode)).ToListAsync(ct),
            await db.Set<ErpSyncRun>().AsNoTracking().OrderByDescending(x => x.Id).Take(50).ToListAsync(ct)));

    public async Task<IActionResult> Operations(CancellationToken ct)
        => View(new OperationsModel(await db.Set<ErpOperationConfiguration>().AsNoTracking().OrderBy(x => x.CompanyCode).ThenBy(x => x.Operation).ToListAsync(ct),
            await db.Set<ErpConnection>().AsNoTracking().OrderBy(x => x.CompanyCode)
                .Select(x => new ErpConnectionListItem(x.Id, x.CompanyCode, x.Code, x.Name, x.Provider, x.Enabled, x.SourceCode)).ToListAsync(ct),
            ErpOperations.ReadOperations));

    [HttpPost]
    public async Task<IActionResult> SaveOperation(OperationModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest("invalid_operation");
        var connection = await db.Set<ErpConnection>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == model.ConnectionId, ct);
        if (connection == null) return NotFound();
        var company = connection.CompanyCode;
        if (!string.Equals(model.CompanyCode.Trim(), company, StringComparison.OrdinalIgnoreCase))
            return BadRequest("connection_company_mismatch");
        try
        {
        await using var lease = await operationLock.AcquireAsync(ErpOperationLock.ProductsKey(company), ct);
        using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var route = await db.Set<ErpOperationConfiguration>().SingleOrDefaultAsync(x => x.CompanyCode == company && x.Operation == model.Operation, ct);
        var isNew = route == null;
        route ??= new ErpOperationConfiguration { CompanyCode = company, Operation = model.Operation };
        route.ConnectionId = connection.Id;
        route.Enabled = model.Enabled;
        route.ScheduleEnabled = model.ScheduleEnabled;
        route.IntervalMinutes = model.IntervalMinutes;
        route.BatchSize = model.BatchSize;
        route.NextOffset = 0;
        route.NextRunUtc = model.Enabled && model.ScheduleEnabled ? DateTime.UtcNow : null;
        route.LastSucceededUtc = null;
        route.Revision = Guid.NewGuid();
        try { router.Validate(route, connection); }
        catch (ErpIntegrationException ex) { return BadRequest(new { error = ex.Code }); }
        if (isNew) db.Set<ErpOperationConfiguration>().Add(route);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return RedirectToAction(nameof(Operations));
        }
        catch (ErpIntegrationException ex) { return Conflict(new { error = ex.Code }); }
    }

    [HttpPost]
    public async Task<IActionResult> PreviewOperation(string companyCode, string operation, CancellationToken ct)
    {
        try
        {
            var route = await router.ResolveAsync(companyCode, operation, ct);
            return Json(await catalog.PreviewAsync(route.Connection, operation, 0, 25, ct));
        }
        catch (ErpIntegrationException ex) { return BadRequest(new { error = ex.Code }); }
    }

    [HttpPost]
    public async Task<IActionResult> RegisterSchedule()
    {
        await ErpSynchronizationTask.EnsureRegisteredAsync(tasks);
        NotifySuccess("ERP scheduler registered. Enable product schedules in Operations.");
        return RedirectToAction(nameof(Operations));
    }

    private void PopulateOptions(ConnectionModel model)
    {
        model.Providers = resolver.Names;
        model.AuthenticationTypes = authentication.Names;
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        if (id == 0)
        {
            var model = new ConnectionModel();
            PopulateOptions(model);
            model.ProviderSettingsJson = resolver.Resolve<IErpProviderConfiguration>(model.Provider).DefaultSettingsJson;
            model.AuthenticationSettingsJson = authentication.Resolve(model.AuthenticationType).DefaultSettingsJson;
            return View(model);
        }
        var c = await db.Set<ErpConnection>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c == null) return NotFound();
        var existing = new ConnectionModel { Id = c.Id, CompanyCode = c.CompanyCode, SourceCode = c.SourceCode, Code = c.Code, Name = c.Name,
            Provider = c.Provider, BaseUrl = c.BaseUrl, Enabled = c.Enabled, TimeoutSeconds = c.TimeoutSeconds,
            ProviderSettingsJson = c.ProviderSettingsJson, AuthenticationType = c.AuthenticationType,
            AuthenticationSettingsJson = c.AuthenticationSettingsJson, HasCredentials = !string.IsNullOrEmpty(c.EncryptedAuthenticationSettings),
            LegacyLoginPayload = c.LegacyLoginPayload };
        PopulateOptions(existing);
        return View(existing);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(ConnectionModel model, CancellationToken ct)
    {
        var current = model.Id == 0 ? new ErpConnection() : await db.Set<ErpConnection>().FirstOrDefaultAsync(x => x.Id == model.Id, ct);
        if (current == null) return NotFound();
        PopulateOptions(model);
        model.HasCredentials = !string.IsNullOrEmpty(current.EncryptedAuthenticationSettings);
        model.LegacyLoginPayload = current.LegacyLoginPayload;
        model.CompanyCode = model.CompanyCode?.Trim();
        model.SourceCode = model.SourceCode?.Trim();
        model.Code = model.Code?.Trim();
        model.Name = model.Name?.Trim();
        var candidate = new ErpConnection
        {
            Id = current.Id, CompanyCode = model.CompanyCode, SourceCode = model.SourceCode, Code = model.Code, Name = model.Name,
            Provider = model.Provider, BaseUrl = model.BaseUrl?.Trim(), Enabled = model.Enabled, TimeoutSeconds = model.TimeoutSeconds,
            AuthenticationType = model.AuthenticationType, ProviderSettingsJson = model.ProviderSettingsJson,
            AuthenticationSettingsJson = model.AuthenticationSettingsJson,
            EncryptedAuthenticationSettings = current.EncryptedAuthenticationSettings, LegacyLoginPayload = current.LegacyLoginPayload
        };
        // Never echo submitted secrets, including failed validation requests.
        var invalidCredentials = new[] { nameof(model.NewUsername), nameof(model.NewPassword) }
            .Any(key => ModelState.TryGetValue(key, out var state) && state.Errors.Count != 0);
        try
        {
            if (invalidCredentials) throw new ErpIntegrationException("invalid_credentials");
            if (string.IsNullOrWhiteSpace(candidate.CompanyCode) || string.IsNullOrWhiteSpace(candidate.SourceCode) || string.IsNullOrWhiteSpace(candidate.Code) || string.IsNullOrWhiteSpace(candidate.Name))
                throw new ErpIntegrationException("invalid_connection_identity");
            var uri = endpoints.ValidateBase(candidate.BaseUrl);
            if (current.Id != 0 && (!string.Equals(current.CompanyCode, candidate.CompanyCode, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(current.Provider, candidate.Provider, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(current.SourceCode, candidate.SourceCode, StringComparison.OrdinalIgnoreCase)))
                throw new ErpIntegrationException("connection_identity_is_immutable");
            candidate.AuthenticationType = authentication.Resolve(candidate.AuthenticationType).SystemName;
            if (current.Id != 0)
            {
                candidate.CompanyCode = current.CompanyCode;
                candidate.SourceCode = current.SourceCode;
                candidate.Provider = current.Provider;
            }
            var replace = !string.IsNullOrWhiteSpace(model.NewUsername) || !string.IsNullOrEmpty(model.NewPassword);
            if (replace && (string.IsNullOrWhiteSpace(model.NewUsername) || string.IsNullOrEmpty(model.NewPassword) || model.ClearCredentials))
                throw new ErpIntegrationException("enter_both_username_and_password");
            if (candidate.AuthenticationType == "None" && replace)
                throw new ErpIntegrationException("credentials_not_needed");
            if (current.Id != 0 && model.HasCredentials &&
                (!string.Equals(endpoints.ValidateBase(current.BaseUrl).AbsoluteUri, uri.AbsoluteUri, StringComparison.Ordinal) ||
                !string.Equals(current.AuthenticationType, candidate.AuthenticationType, StringComparison.OrdinalIgnoreCase) ||
                (current.LegacyLoginPayload && current.AuthenticationSettingsJson != candidate.AuthenticationSettingsJson)) &&
                !replace && !model.ClearCredentials)
                throw new ErpIntegrationException("replace_credentials_when_changing_authentication");
            if (model.ClearCredentials || candidate.AuthenticationType == "None")
            {
                if (candidate.AuthenticationType == "None") candidate.AuthenticationSettingsJson = "{}";
                candidate.EncryptedAuthenticationSettings = null;
                candidate.LegacyLoginPayload = false;
            }
            else if (replace)
            {
                candidate.EncryptedAuthenticationSettings = secrets.Protect(ErpSettingsJson.Write(new ErpUsernamePassword
                { Username = model.NewUsername, Password = model.NewPassword }));
                candidate.LegacyLoginPayload = false;
            }
            resolver.Resolve<IErpProviderConfiguration>(candidate.Provider).Validate(candidate);
            var auth = authentication.Resolve(candidate.AuthenticationType);
            auth.Validate(candidate);
            if (await db.Set<ErpConnection>().AnyAsync(x => x.Id != candidate.Id && x.CompanyCode == candidate.CompanyCode && x.Code == candidate.Code, ct))
                throw new ErpIntegrationException("duplicate_connection_code");
        }
        catch (ErpIntegrationException ex) { ModelState.AddModelError("", ex.Code); }
        finally
        {
            model.NewUsername = null;
            model.NewPassword = null;
            ModelState.Remove(nameof(model.NewUsername));
            ModelState.Remove(nameof(model.NewPassword));
        }
        if (!ModelState.IsValid) return View(model);
        try
        {
            await using var lease = await operationLock.AcquireAsync(ErpOperationLock.ProductsKey(candidate.CompanyCode), ct);
            if (current.Id == 0) db.Set<ErpConnection>().Add(candidate);
            else
            {
                var version = await db.Set<ErpConnection>().AsNoTracking().Where(x => x.Id == current.Id)
                    .Select(x => x.ConfigurationVersion).SingleAsync(ct);
                if (version != current.ConfigurationVersion) return Conflict("connection_changed_reload");
                db.Entry(current).CurrentValues.SetValues(candidate);
            }
            await db.SaveChangesAsync(ct);
        }
        catch (ErpIntegrationException ex) { return Conflict(new { error = ex.Code }); }
        return RedirectToAction(nameof(Index));
    }

    // Explicit read-only test of saved configuration, even when disabled.
    [HttpPost]
    public async Task<IActionResult> TestConnection(int id, string operation, CancellationToken ct)
    {
        var c = await db.Set<ErpConnection>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c == null) return NotFound();
        c.Enabled = true; // detached instance only
        try { await catalog.PreviewAsync(c, operation ?? "Products", 0, 1, ct); return Json(new { success = true }); }
        catch (ErpIntegrationException ex) { return BadRequest(new { error = ex.Code }); }
    }

    [HttpPost]
    public async Task<IActionResult> Preview(int id, string operation, int skip, int take, CancellationToken ct)
    {
        var c = await db.Set<ErpConnection>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c == null) return NotFound();
        try { return Json(await catalog.PreviewAsync(c, operation, skip, take, ct)); }
        catch (ErpIntegrationException ex) { return BadRequest(new { error = ex.Code }); }
    }

    [HttpPost]
    public async Task<IActionResult> StageProducts(int id, int skip, int take, CancellationToken ct)
    {
        var c = await db.Set<ErpConnection>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c == null) return NotFound();
        try { var count = await runner.StageProductsAsync(c.CompanyCode, c.Id, skip, take, ct); NotifySuccess($"Staged {count} products."); }
        catch (ErpIntegrationException ex) { NotifyError(ex.Code); }
        return RedirectToAction(nameof(Catalog), new { id });
    }

    public async Task<IActionResult> Catalog(int id, int skip, CancellationToken ct)
    {
        if (skip < 0) return BadRequest();
        var connection = await db.Set<ErpConnection>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (connection == null) return NotFound();
        var rows = await db.Set<ErpCatalogProduct>().AsNoTracking().Where(x => x.CompanyCode == connection.CompanyCode && x.SourceCode == connection.SourceCode)
            .OrderBy(x => x.Id).Skip(skip).Take(100).ToListAsync(ct);
        var templates = await db.Set<ProductTemplate>().AsNoTracking().OrderBy(x => x.DisplayOrder)
            .Select(x => new ProductTemplateOption(x.Id, x.Name)).ToListAsync(ct);
        return View(new CatalogModel(id, skip, rows, templates));
    }

    [HttpPost]
    [Permission(Permissions.Catalog.Product.Create)]
    public async Task<IActionResult> CreateDraft(int id, int templateId, CancellationToken ct)
    {
        try
        {
            var result = await drafts.CreateAsync(id, templateId, ct);
            NotifySuccess("Unpublished draft created; price, tax, inventory and unit require review.");
            return RedirectToAction(nameof(Catalog), new { id = result.ConnectionId });
        }
        catch (ErpIntegrationException ex) { return BadRequest(new { error = ex.Code }); }
    }

    [HttpPost]
    [Permission(Permissions.Catalog.Product.Update)]
    public async Task<IActionResult> LinkProduct(int id, int productId, CancellationToken ct)
    {
        using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var row = await db.Set<ErpCatalogProduct>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row == null) return NotFound();
        if (productId <= 0 || !await db.Products.AnyAsync(x => x.Id == productId && !x.Deleted && x.ProductTypeId == (int)ProductType.SimpleProduct, ct))
            return BadRequest("invalid_product");
        if (row.ProductId.HasValue && row.ProductId != productId)
            return Conflict("mapping_already_exists");
        if (await db.Set<ErpCatalogProduct>().AnyAsync(x => x.ProductId == productId && x.Id != id, ct))
            return Conflict("product_already_mapped");
        row.ProductId = productId;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return RedirectToAction(nameof(Catalog), new { id = row.ConnectionId });
    }
}
