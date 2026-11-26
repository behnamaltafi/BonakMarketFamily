using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;
using BonakMarket.Core.Security;
using BonakMarket.ERP.Domain;
using BonakMarket.ERP.Models;
using BonakMarket.ERP.Providers.Varanegar;
using BonakMarket.ERP.Services;
using BonakMarket.Web.Controllers;

namespace BonakMarket.ERP.Controllers;

[Permission("configuration.setting")]
public sealed class ErpController(SmartDbContext db, ErpProviderResolver resolver, ErpSecrets secrets, ErpCatalogService catalog) : AdminController
{
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(new ErpIndexModel(await db.Set<ErpConnection>().AsNoTracking().OrderBy(x => x.CompanyCode)
            .Select(x => new ErpConnectionListItem(x.Id, x.CompanyCode, x.Code, x.Name, x.Provider, x.Enabled)).ToListAsync(ct),
            await db.Set<ErpSyncRun>().AsNoTracking().OrderByDescending(x => x.Id).Take(50).ToListAsync(ct)));

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        if (id == 0) return View(new ConnectionModel { Providers = resolver.Names });
        var c = await db.Set<ErpConnection>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c == null) return NotFound();
        return View(new ConnectionModel { Id = c.Id, CompanyCode = c.CompanyCode, Code = c.Code, Name = c.Name,
            Provider = c.Provider, BaseUrl = c.BaseUrl, Enabled = c.Enabled, TimeoutSeconds = c.TimeoutSeconds,
            ODataPrefix = c.ODataPrefix, LoginPath = c.LoginPath, AuthorizationScheme = c.AuthorizationScheme,
            UseLogin = c.UseLogin, HasLoginBody = !string.IsNullOrEmpty(c.EncryptedLoginBody), Providers = resolver.Names });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(ConnectionModel model, CancellationToken ct)
    {
        var c = model.Id == 0 ? new ErpConnection() : await db.Set<ErpConnection>().FirstOrDefaultAsync(x => x.Id == model.Id, ct);
        if (c == null) return NotFound();
        model.Providers = resolver.Names;
        var secret = c.EncryptedLoginBody;
        model.CompanyCode = model.CompanyCode?.Trim();
        model.Code = model.Code?.Trim();
        model.Name = model.Name?.Trim();
        var invalidSecret = ModelState.TryGetValue(nameof(model.NewLoginBody), out var secretState) && secretState.Errors.Count != 0;
        if (invalidSecret) ModelState.AddModelError("", "invalid_login_body");
        try
        {
            if (string.IsNullOrWhiteSpace(model.CompanyCode) || string.IsNullOrWhiteSpace(model.Code) || string.IsNullOrWhiteSpace(model.Name))
                throw new ErpIntegrationException("invalid_connection_identity");
            var uri = VaranegarProvider.ValidateBase(model.BaseUrl);
            if (!string.IsNullOrWhiteSpace(model.ODataPrefix)) VaranegarProvider.Relative(uri, model.ODataPrefix);
            if (c.Id != 0 && !string.IsNullOrEmpty(secret) && !string.Equals(VaranegarProvider.ValidateBase(c.BaseUrl).AbsoluteUri, uri.AbsoluteUri, StringComparison.Ordinal) &&
                !model.ClearLoginBody && string.IsNullOrWhiteSpace(model.NewLoginBody))
                throw new ErpIntegrationException("replace_credentials_when_changing_base_url");
            VaranegarProvider.Relative(uri, model.LoginPath);
            if (!string.IsNullOrEmpty(model.AuthorizationScheme) && !Regex.IsMatch(model.AuthorizationScheme, "^[A-Za-z][A-Za-z0-9_-]{0,39}$"))
                throw new ErpIntegrationException("invalid_authorization_scheme");
            if (!resolver.Names.Contains(model.Provider, StringComparer.OrdinalIgnoreCase))
                throw new ErpIntegrationException("unsupported_provider");
            if (model.ClearLoginBody) secret = null;
            else if (!invalidSecret && !string.IsNullOrWhiteSpace(model.NewLoginBody))
            {
                using var doc = JsonDocument.Parse(model.NewLoginBody);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new ErpIntegrationException("login_body_must_be_object");
                secret = secrets.Protect(model.NewLoginBody);
            }
            if (model.Enabled && model.UseLogin && string.IsNullOrEmpty(secret))
                throw new ErpIntegrationException("login_not_configured");
            // Company and provider identity cannot be reassigned after creation.
            if (c.Id != 0 && (!string.Equals(c.CompanyCode, model.CompanyCode, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(c.Provider, model.Provider, StringComparison.OrdinalIgnoreCase)))
                throw new ErpIntegrationException("connection_identity_is_immutable");
            if (await db.Set<ErpConnection>().AnyAsync(x => x.Id != model.Id && x.CompanyCode == model.CompanyCode && x.Code == model.Code, ct))
                throw new ErpIntegrationException("duplicate_connection_code");
        }
        catch (ErpIntegrationException ex) { ModelState.AddModelError("", ex.Code); }
        catch (JsonException) { ModelState.AddModelError("", "invalid_login_json"); }
        model.NewLoginBody = null;
        ModelState.Remove(nameof(model.NewLoginBody));
        model.HasLoginBody = !string.IsNullOrEmpty(c.EncryptedLoginBody);
        if (!ModelState.IsValid) return View(model);
        c.CompanyCode = model.CompanyCode.Trim();
        c.Code = model.Code.Trim();
        c.Name = model.Name.Trim();
        c.Provider = model.Provider;
        c.BaseUrl = model.BaseUrl.Trim();
        c.Enabled = model.Enabled;
        c.TimeoutSeconds = model.TimeoutSeconds;
        c.ODataPrefix = model.ODataPrefix?.Trim() ?? "";
        c.LoginPath = model.LoginPath.Trim();
        c.AuthorizationScheme = model.AuthorizationScheme;
        c.UseLogin = model.UseLogin;
        c.EncryptedLoginBody = secret;
        if (c.Id == 0) db.Set<ErpConnection>().Add(c);
        await db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Index));
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
        try { var count = await catalog.StageProductsAsync(c, skip, take, ct); NotifySuccess($"Staged {count} products."); }
        catch (ErpIntegrationException ex) { NotifyError(ex.Code); }
        return RedirectToAction(nameof(Catalog), new { id });
    }

    public async Task<IActionResult> Catalog(int id, int skip, CancellationToken ct)
    {
        if (skip < 0 || !await db.Set<ErpConnection>().AnyAsync(x => x.Id == id, ct)) return BadRequest();
        var rows = await db.Set<ErpCatalogProduct>().AsNoTracking().Where(x => x.ConnectionId == id)
            .OrderBy(x => x.Id).Skip(skip).Take(100).ToListAsync(ct);
        return View(new CatalogModel(id, skip, rows));
    }

    [HttpPost]
    public async Task<IActionResult> LinkProduct(int id, int productId, CancellationToken ct)
    {
        using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var row = await db.Set<ErpCatalogProduct>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row == null) return NotFound();
        if (productId <= 0 || !await db.Products.AnyAsync(x => x.Id == productId && !x.Deleted, ct))
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
