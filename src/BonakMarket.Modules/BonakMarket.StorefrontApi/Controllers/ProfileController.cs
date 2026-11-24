using Microsoft.AspNetCore.Mvc;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1/profile")]
[StorefrontPermission(StorefrontPermissions.Profile)]
public sealed class ProfileController(ProfileFacade profile, CustomerQueries queries) : StorefrontController
{
    [HttpGet]
    public async Task<ActionResult<ProfileDto>> Get(CancellationToken ct) => Ok(await profile.GetAsync(ct));

    [HttpPut]
    public async Task<ActionResult<ProfileDto>> Update([FromBody] UpdateProfileRequest input, CancellationToken ct)
    {
        if (!profile.CanUpdate) return Error(503, "sms_profile_unavailable");
        var result = await profile.UpdateAsync(input, ct);
        if (!result.Succeeded)
        {
            var problem = new ProblemDetails { Status = 422, Title = "invalid_profile" };
            problem.Extensions["errors"] = result.Errors.Select(x => x.Description).ToArray();
            return new ObjectResult(problem) { StatusCode = 422, ContentTypes = { "application/problem+json" } };
        }
        return Ok(await profile.GetAsync(ct));
    }

    [HttpGet("addresses")]
    public async Task<ActionResult<List<AddressDto>>> Addresses(CancellationToken ct) => Ok(await queries.AddressesAsync(ct));
}
