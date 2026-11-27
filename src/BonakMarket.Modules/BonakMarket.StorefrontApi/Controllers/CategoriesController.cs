using Microsoft.AspNetCore.Mvc;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1/categories")]
[StorefrontPermission(StorefrontPermissions.Catalog)]
public sealed class CategoriesController(CatalogQueries queries) : StorefrontController
{
    [HttpGet, PublicCatalog]
    public async Task<ActionResult<PageDto<CategoryDto>>> Get([FromQuery] CategorySearchRequest filter, CancellationToken ct) =>
        Ok(await queries.CategoriesAsync(filter, ct));

    [HttpGet("{id:int}"), PublicCatalog]
    public async Task<ActionResult<CategoryDto>> GetById(int id, CancellationToken ct)
    {
        var item = await queries.CategoryAsync(id, ct);
        if (item == null) return Error(404, "category_not_found");
        return Ok(item);
    }
}
