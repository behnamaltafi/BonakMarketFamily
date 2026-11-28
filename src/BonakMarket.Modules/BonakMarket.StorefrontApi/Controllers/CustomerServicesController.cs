using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Catalog.Search;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1/me")]
public sealed class CustomerServicesController(CustomerStorefrontService customerService) : StorefrontController
{
    [HttpGet("summary")]
    public async Task<ActionResult<CustomerSummaryDto>> Summary(CancellationToken ct) =>
        Ok(await customerService.GetSummaryAsync(ct));

    /// <summary>Products frequently purchased by the authenticated customer in this store.</summary>
    [HttpGet("frequently-purchased")]
    [StorefrontPermission(StorefrontPermissions.Orders)]
    [StorefrontPermission(StorefrontPermissions.Catalog)]
    public async Task<ActionResult<PageDto<ProductDto>>> FrequentlyPurchased([FromQuery] ProductFilterRequest filter,
        [FromServices] CatalogFacade catalog, [FromServices] SearchSettings searchSettings, CancellationToken ct)
    {
        if (filter.RequiresPriceAccess && !await catalog.CanDisplayPriceAsync())
            return Error(403, "price_access_denied");
        return Ok(await catalog.FrequentlyPurchasedAsync(filter, ct, searchSettings));
    }

    [HttpGet("reviews")]
    [StorefrontPermission(StorefrontPermissions.Reviews)]
    [StorefrontPermission(StorefrontPermissions.Catalog)]
    public async Task<ActionResult<PageDto<MyReviewDto>>> MyReviews([FromQuery] PageRequest page, CancellationToken ct) =>
        Ok(await customerService.GetReviewsAsync(page, ct));
}
