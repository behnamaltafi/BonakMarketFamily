using Microsoft.AspNetCore.Mvc;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1/products")]
[StorefrontPermission(StorefrontPermissions.Catalog)]
public sealed class CatalogController(CatalogFacade catalog) : StorefrontController
{
    [HttpGet, PublicCatalog]
    public async Task<ActionResult<PageDto<ProductDto>>> Search([FromQuery] ProductSearchRequest filter, CancellationToken ct)
    {
        if ((filter.MinPrice.HasValue || filter.MaxPrice.HasValue || filter.HasDiscount.HasValue ||
            filter.Sort is CatalogSort.PriceAsc or CatalogSort.PriceDesc) && !await catalog.CanDisplayPriceAsync())
            return Error(403, "price_access_denied");
        return Ok(await catalog.SearchAsync(filter, ct));
    }

    [HttpGet("facets"), PublicCatalog]
    public async Task<ActionResult<CatalogFacetsDto>> Facets([FromQuery] ProductSearchRequest filter, CancellationToken ct)
    {
        if ((filter.MinPrice.HasValue || filter.MaxPrice.HasValue || filter.HasDiscount.HasValue ||
            filter.Sort is CatalogSort.PriceAsc or CatalogSort.PriceDesc) && !await catalog.CanDisplayPriceAsync())
            return Error(403, "price_access_denied");
        return Ok(await catalog.FacetsAsync(filter, ct));
    }

    [HttpGet("{id:int}"), PublicCatalog]
    public async Task<ActionResult<ProductDetailDto>> Get(int id, CancellationToken ct)
    {
        var item = await catalog.DetailAsync(id, ct);
        if (item == null) return Error(404, "product_not_found");
        return Ok(item);
    }

    /// <summary>Read-only price and validation for an explicit selection; does not add items to the cart.</summary>
    [HttpPost("{id:int}/quote"), PublicCatalog]
    public async Task<ActionResult<ProductQuoteDto>> Quote(int id, [FromBody] ProductQuoteRequest input, CancellationToken ct)
    {
        var result = await catalog.QuoteAsync(id, input, ct);
        return result.Status == 200 ? Ok(result.Quote) : Error(result.Status, result.Code);
    }

    [HttpGet("{id:int}/variants"), PublicCatalog]
    public async Task<ActionResult<List<VariantDto>>> Variants(int id, CancellationToken ct)
    {
        var variants = await catalog.VariantsAsync(id, ct);
        if (variants == null) return Error(404, "product_not_found");
        return Ok(variants);
    }

    [HttpGet("{id:int}/price"), PublicCatalog]
    public async Task<ActionResult<PriceDto>> Price(int id, [FromQuery] int quantity = 1, CancellationToken ct = default)
    {
        if (quantity < 1 || quantity > 1000) return Error(400, "invalid_quantity");
        if (!await catalog.CanDisplayPriceAsync()) return Error(403, "price_access_denied");
        var price = await catalog.PriceAsync(id, quantity, ct);
        if (price == null) return Error(404, "price_unavailable");
        return Ok(price);
    }
}
