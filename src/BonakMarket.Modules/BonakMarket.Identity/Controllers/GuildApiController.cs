using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;
using BonakMarket.Identity.Models;

namespace BonakMarket.Identity.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = "identity")]
[Route("api/identity/v1/guilds")]
public sealed class GuildApiController(SmartDbContext db) : ControllerBase
{
    [HttpGet, AllowAnonymous]
    [ProducesResponseType(typeof(List<GuildOptionDto>), 200)]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await db.Guilds.AsNoTracking()
        .Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).ThenBy(x => x.Title)
        .Select(x => new GuildOptionDto(x.Id, x.Title)).ToListAsync(ct));
}
