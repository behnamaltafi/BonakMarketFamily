using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Security;

namespace BonakMarket.Admin.Controllers;

public sealed class GuildController(SmartDbContext db) : AdminController
{
    [Permission(Permissions.Customer.Guild.Read)]
    public async Task<IActionResult> List(string search, CancellationToken ct)
    {
        ViewBag.Search = search;
        ViewBag.Counts = await db.Customers.AsNoTracking().Where(c => c.GuildId != null)
            .GroupBy(c => c.GuildId.Value).Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        ViewBag.CanCreate = await Services.Permissions.AuthorizeAsync(Permissions.Customer.Guild.Create);
        ViewBag.CanUpdate = await Services.Permissions.AuthorizeAsync(Permissions.Customer.Guild.Update);
        ViewBag.CanDelete = await Services.Permissions.AuthorizeAsync(Permissions.Customer.Guild.Delete);
        return View(await new GuildService(db).SearchAsync(search, ct));
    }

    [HttpPost, ValidateAntiForgeryToken, Permission(Permissions.Customer.Guild.Create)]
    public async Task<IActionResult> Create(string code, string title, bool active, int displayOrder, CancellationToken ct)
    {
        var error = await new GuildService(db).SaveAsync(0, code, title, active, displayOrder, ct);
        if (error != null) NotifyError(error);
        return RedirectToAction(nameof(List));
    }

    [HttpPost, ValidateAntiForgeryToken, Permission(Permissions.Customer.Guild.Update)]
    public async Task<IActionResult> Edit(int id, string code, string title, bool active, int displayOrder, CancellationToken ct)
    {
        if (id < 1) return BadRequest();
        var error = await new GuildService(db).SaveAsync(id, code, title, active, displayOrder, ct);
        if (error != null) NotifyError(error);
        return RedirectToAction(nameof(List));
    }

    [HttpPost, ValidateAntiForgeryToken, Permission(Permissions.Customer.Guild.Delete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var error = await new GuildService(db).DeleteAsync(id, ct);
        if (error != null) NotifyError(error);
        return RedirectToAction(nameof(List));
    }

    [Permission(Permissions.Customer.Read)]
    public async Task<IActionResult> Customers(int id, int page = 1, CancellationToken ct = default)
    {
        var guild = await db.Guilds.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (guild == null) return NotFound();
        page = Math.Clamp(page, 1, 1000000);
        ViewBag.Guild = guild.Title; ViewBag.GuildId = id; ViewBag.Page = page;
        var query = db.Customers.AsNoTracking().Where(c => c.GuildId == id && !c.IsSystemAccount);
        ViewBag.Total = await query.CountAsync(ct);
        return View(await query.OrderBy(c => c.Id).Skip((page - 1) * 50).Take(50).ToListAsync(ct));
    }
}
