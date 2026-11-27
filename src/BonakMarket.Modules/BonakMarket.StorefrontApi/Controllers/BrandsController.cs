using Microsoft.AspNetCore.Mvc;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1/brands")]
[StorefrontPermission(StorefrontPermissions.Catalog)]
public sealed class BrandsController(CatalogQueries queries) : StorefrontController
{
    [HttpGet, PublicCatalog]
    public async Task<ActionResult<PageDto<BrandDto>>> Get([FromQuery] BrandSearchRequest filter, CancellationToken ct) =>
        Ok(await queries.BrandsAsync(filter, ct));

    [HttpGet("{id:int}"), PublicCatalog]
    public async Task<ActionResult<BrandDto>> GetById(int id, CancellationToken ct)
    {
        var item = await queries.BrandAsync(id, ct);
        if (item == null) return Error(404, "brand_not_found");
        return Ok(item);
    }
}
