using Microsoft.AspNetCore.Mvc;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1")]
[StorefrontPermission(StorefrontPermissions.Returns)]
[StorefrontPermission(StorefrontPermissions.Orders)]
public sealed class ReturnsController(ReturnStorefrontService returns) : StorefrontController
{
    [HttpGet("returns")]
    public async Task<ActionResult<PageDto<ReturnCaseDto>>> List([FromQuery] PageRequest page, CancellationToken ct) =>
        Ok(await returns.ListAsync(page, ct));

    [HttpGet("returns/{id:int}")]
    public async Task<ActionResult<ReturnCaseDto>> Get(int id, CancellationToken ct) =>
        Respond(await returns.GetAsync(id, ct));

    [HttpGet("orders/{id:int}/return-options")]
    public async Task<ActionResult<ReturnOptionsDto>> Options(int id, CancellationToken ct) =>
        Respond(await returns.OptionsAsync(id, ct));

    [HttpPost("orders/{id:int}/returns")]
    public async Task<ActionResult<List<int>>> Create(int id, [FromBody] ReturnRequest input, CancellationToken ct) =>
        Respond(await returns.CreateAsync(id, input, ct), 201);
}
