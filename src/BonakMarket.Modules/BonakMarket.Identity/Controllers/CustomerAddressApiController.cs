using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Identity;
using BonakMarket.Identity.Models;
using BonakMarket.Identity.Services;

namespace BonakMarket.Identity.Controllers;

[ApiController, ApiExplorerSettings(GroupName = "identity")]
[Route("api/identity/v1/me/addresses")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CustomerAddressApiController(CustomerAddressService addresses, SignInManager<Customer> signIn,
    UserManager<Customer> users, IAntiforgery antiforgery) : CustomerAccountApiController(signIn, users, antiforgery)
{
    [HttpGet, AllowAnonymous]
    [ProducesResponseType(typeof(AddressPage), 200)]
    public async Task<IActionResult> List(int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var (customer, error) = await RequireAccountAsync();
        if (error != null) return error;
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue) return BadRequest();
        return Ok(await addresses.ListAsync(customer, page, pageSize, ct));
    }

    [HttpGet("~/api/sms-login/profile/addresses"), AllowAnonymous]
    public async Task<IActionResult> LegacyList(CancellationToken ct)
    {
        var (customer, error) = await RequireAccountAsync();
        return error ?? Ok(await addresses.LegacyListAsync(customer, ct));
    }

    [HttpGet("settings"), AllowAnonymous]
    [ProducesResponseType(typeof(CustomerAddressSettings), 200)]
    public async Task<IActionResult> Settings(CancellationToken ct)
    {
        var (_, error) = await RequireAccountAsync();
        if (error != null) return error;
        var result = await addresses.GetSettingsAsync(ct);
        return result == null ? Conflict(new { code = "iran_not_configured" }) : Ok(result);
    }

    [HttpGet("{id:int}"), AllowAnonymous]
    [ProducesResponseType(typeof(CustomerAddressDto), 200)]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var (customer, error) = await RequireAccountAsync();
        if (error != null) return error;
        var result = await addresses.GetAsync(customer, id, ct);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost, AllowAnonymous, IgnoreAntiforgeryToken]
    [ProducesResponseType(typeof(CustomerAddressDto), 201)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 422)]
    public Task<IActionResult> Create([FromBody] AddressUpdateRequest request, CancellationToken ct) => Save(null, request, ct);

    [HttpPut("{id:int}"), AllowAnonymous, IgnoreAntiforgeryToken]
    [ProducesResponseType(typeof(CustomerAddressDto), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 422)]
    public Task<IActionResult> Update(int id, [FromBody] AddressUpdateRequest request, CancellationToken ct) => Save(id, request, ct);

    [HttpPut("{id:int}/default"), AllowAnonymous, IgnoreAntiforgeryToken]
    [ProducesResponseType(typeof(CustomerAddressDto), 200)]
    public async Task<IActionResult> SetDefault(int id, CancellationToken ct)
    {
        var (customer, error) = await RequireAccountAsync();
        if (error != null) return error;
        var status = await addresses.DefaultAsync(customer, id, ct);
        return status == 200 ? Ok(await addresses.GetAsync(customer, id, ct)) : StatusCode(status);
    }

    [HttpDelete("{id:int}"), AllowAnonymous, IgnoreAntiforgeryToken]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var (customer, error) = await RequireAccountAsync();
        if (error != null) return error;
        return StatusCode(await addresses.DeleteAsync(customer, id, ct));
    }

    private async Task<IActionResult> Save(int? id, AddressUpdateRequest request, CancellationToken ct)
    {
        var (customer, error) = await RequireAccountAsync();
        if (error != null) return error;
        var result = await addresses.SaveAsync(customer, id, request, ct);
        if (result.Status == 404) return NotFound();
        if (result.Errors != null)
            return new ObjectResult(new ValidationProblemDetails(result.Errors)
                { Status = result.Status, Title = "validation_failed" }) { StatusCode = result.Status };
        return id.HasValue ? Ok(result.Address) : CreatedAtAction(nameof(Get), new { id = result.Address.Id }, result.Address);
    }
}
