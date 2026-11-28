using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BonakMarket.Identity.Models;
using BonakMarket.Identity.Services;

namespace BonakMarket.Identity.Controllers;

[ApiController, ApiExplorerSettings(GroupName = "identity")]
[Route("api/identity/v1/locations")]
public sealed class LocationController(CustomerAddressService addresses) : ControllerBase
{
    [HttpGet("provinces"), AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<AddressLocation>>> Provinces(CancellationToken ct)
        => Ok(await addresses.ProvincesAsync(ct));

    [HttpGet("provinces/{id:int}/cities"), AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<CityLocation>>> Cities(int id, CancellationToken ct)
        => Ok(await addresses.CitiesAsync(id, ct));
}
