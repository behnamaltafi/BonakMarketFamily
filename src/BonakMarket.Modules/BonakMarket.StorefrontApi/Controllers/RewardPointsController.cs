using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1/reward-points")]
[StorefrontPermission(StorefrontPermissions.RewardPoints)]
public sealed class RewardPointsController(SmartDbContext db, StorefrontRequestContext request, RewardPointsSettings settings) : StorefrontController
{
    [HttpGet]
    public async Task<ActionResult<RewardPointsDto>> Get([FromQuery] PageRequest page, CancellationToken ct)
    {
        if (!settings.Enabled) return Error(403, "reward_points_disabled");
        var query = db.RewardPointsHistory.AsNoTracking().Where(x => x.CustomerId == request.Customer.Id)
            .OrderByDescending(x => x.CreatedOnUtc).ThenByDescending(x => x.Id);
        var balance = await query.Select(x => (int?)x.PointsBalance).FirstOrDefaultAsync(ct) ?? 0;
        var history = await query.Select(x => new RewardPointDto(x.Id, x.Points, x.PointsBalance,
            x.UsedAmount, x.Message, x.CreatedOnUtc)).PageAsync(page, ct);
        return Ok(new RewardPointsDto(balance, history));
    }
}
