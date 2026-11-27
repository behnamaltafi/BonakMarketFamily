using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Catalog.Search;
using BonakMarket.Core.Common.Services;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1/products")]
[StorefrontPermission(StorefrontPermissions.Catalog)]
public sealed class CatalogController(CatalogFacade catalog) : StorefrontController
{
    /// <summary>List and search the visible catalog with filters and pagination.</summary>
    [HttpGet, PublicCatalog]
    public async Task<ActionResult<PageDto<ProductDto>>> Search([FromQuery] ProductSearchRequest filter, [FromServices] SearchSettings settings, CancellationToken ct)
    {
        if ((filter.MinPrice.HasValue || filter.MaxPrice.HasValue || filter.HasDiscount.HasValue ||
            filter.Sort is CatalogSort.PriceAsc or CatalogSort.PriceDesc) && !await catalog.CanDisplayPriceAsync())
            return Error(403, "price_access_denied");
        return Ok(await catalog.SearchAsync(filter, ct, settings));
    }

    /// <summary>Lightweight suggestions for the current search term. No price calculation or full result count.</summary>
    [HttpGet("suggestions"), PublicCatalog]
    public async Task<ActionResult<InstantSearchDto>> InstantSearch([FromQuery] InstantSearchRequest filter,
        [FromServices] SearchSettings settings, CancellationToken ct)
        => Ok(await catalog.InstantSearchAsync(filter, settings, ct));

    /// <summary>Available category and brand counts for the filtered product set.</summary>
    [HttpGet("facets"), PublicCatalog]
    public async Task<ActionResult<CatalogFacetsDto>> Facets([FromQuery] ProductFacetsRequest filter, [FromServices] SearchSettings settings,
        [FromServices] ICurrencyService currencies, CancellationToken ct)
    {
        var canFilterPrice = await catalog.CanDisplayPriceAsync();
        if ((filter.MinPrice.HasValue || filter.MaxPrice.HasValue || filter.HasDiscount.HasValue) && !canFilterPrice)
            return Error(403, "price_access_denied");
        var currencyCode = currencies.PrimaryCurrency.CurrencyCode;
        var result = await catalog.FacetsAsync(filter, ct, settings, canFilterPrice ? currencyCode : null);
        return Ok(result with { CanFilterPrice = canFilterPrice, PriceFilterCurrencyCode = currencyCode });
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
}
