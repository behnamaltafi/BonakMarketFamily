using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;

namespace BonakMarket.Core.Identity;

public sealed class GuildService(SmartDbContext db)
{
    public async Task<List<Guild>> SearchAsync(string search, CancellationToken ct)
    {
        var query = db.Guilds.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(g => g.Code.Contains(search) || g.Title.Contains(search));
        }
        return await query.OrderBy(g => g.DisplayOrder).ThenBy(g => g.Title).ToListAsync(ct);
    }

    public async Task<string> SaveAsync(int id, string code, string title, bool active, int displayOrder, CancellationToken ct)
    {
        code = code?.Trim(); title = title?.Trim();
        if (string.IsNullOrEmpty(code) || code.Length > 50 || string.IsNullOrEmpty(title) || title.Length > 225 || displayOrder < 0)
            return "invalid_guild";
        if (await db.Guilds.AnyAsync(x => x.Id != id && x.Code == code, ct)) return "duplicate_guild_code";
        var guild = id == 0 ? new Guild { CreatedOnUtc = DateTime.UtcNow } : await db.Guilds.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (guild == null) return "guild_not_found";
        guild.Code = code; guild.Title = title; guild.IsActive = active; guild.DisplayOrder = displayOrder;
        if (id == 0) db.Guilds.Add(guild); else guild.UpdatedOnUtc = DateTime.UtcNow;
        try { await db.SaveChangesAsync(ct); return null; }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return "guild_conflict"; }
    }

    public async Task<string> DeleteAsync(int id, CancellationToken ct)
    {
        var guild = await db.Guilds.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (guild == null) return "guild_not_found";
        if (await db.Customers.IgnoreQueryFilters().AnyAsync(x => x.GuildId == id, ct)) return "guild_in_use";
        db.Guilds.Remove(guild);
        try { await db.SaveChangesAsync(ct); return null; }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return "guild_in_use"; }
    }
}
