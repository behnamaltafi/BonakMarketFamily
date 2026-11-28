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
    public async Task<ActionResult<LocationPage<AddressLocation>>> Provinces([FromQuery] LocationListRequest request, CancellationToken ct)
        => Ok(await addresses.ProvincesAsync(request, ct));

    [HttpGet("provinces/{provinceId:int}/cities"), AllowAnonymous]
    public async Task<ActionResult<LocationPage<CityLocation>>> Cities(int provinceId, [FromQuery] LocationListRequest request, CancellationToken ct)
        => Ok(await addresses.CitiesAsync(provinceId, request, ct));
}
