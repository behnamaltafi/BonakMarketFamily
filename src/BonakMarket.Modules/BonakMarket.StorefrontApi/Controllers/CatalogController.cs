using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Common.Paging;
using BonakMarket.Core.Catalog.Brands;
using BonakMarket.Core.Catalog.Categories;
using BonakMarket.Core.Catalog.Pricing;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Content.Media;
using Common.DTO;
using BonakMarket.Core.Data;
using BonakMarket.Core.Security;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1")]
public sealed class CatalogController(SmartDbContext db, IPriceCalculationService prices, IMediaService media) : StorefrontController
{
    private IQueryable<ProductDto> Products(int? categoryId = null, int? brandId = null)
    {
        var q = StorefrontScope.Products(db, Customer, StoreId).AsNoTracking();
        if (categoryId.HasValue)
        {
            var allowed = StorefrontScope.Categories(db, Customer, StoreId).Where(x => x.Id == categoryId.Value).Select(x => x.Id);
            var ids = db.Set<ProductCategory>().Where(x => allowed.Contains(x.CategoryId)).Select(x => x.ProductId);
            q = q.Where(x => ids.Contains(x.Id));
        }
        if (brandId.HasValue)
        {
            var allowed = StorefrontScope.Brands(db, Customer, StoreId).Where(x => x.Id == brandId.Value).Select(x => x.Id);
            var ids = db.Set<ProductManufacturer>().Where(x => allowed.Contains(x.ManufacturerId)).Select(x => x.ProductId);
            q = q.Where(x => ids.Contains(x.Id));
        }
        return q.Select(x => new ProductDto { Id = x.Id, Name = x.Name, ShortDescription = x.ShortDescription,
            MediaFileId = x.ProductMediaFiles.Where(m => !m.MediaFile.Deleted && !m.MediaFile.Hidden && !m.MediaFile.IsTransient)
                .OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => (int?)m.MediaFileId).FirstOrDefault() });
    }

    [HttpPost("products/search"), PublicCatalog]
    [ProducesResponseType(typeof(ApiResult<PagingResult<ProductDto>>), 200)]
    public async Task<IActionResult> Search([FromBody] PagingFilter filter, [FromQuery] int? categoryId, [FromQuery] int? brandId, [FromQuery] bool includePrice = false)
    {
        if (!StorefrontPaging.Validate<ProductDto>(filter, "Id", "Name", "ShortDescription"))
            return Error(400, "invalid_paging");
        var result = await Products(categoryId, brandId).FilterPaging(filter);
        await Enrich(result.Items, includePrice);
        return Success(result);
    }

    [HttpGet("products/{id:int}"), PublicCatalog]
    [ProducesResponseType(typeof(ApiResult<ProductDto>), 200)]
    public async Task<IActionResult> Product(int id)
    {
        var item = await Products().FirstOrDefaultAsync(x => x.Id == id);
        if (item == null) return Error(404, "product_not_found");
        await Enrich(new[] { item }, true);
        return Success(item);
    }

    private async Task Enrich(IEnumerable<ProductDto> items, bool includePrice)
    {
        var list = items.ToList();
        foreach (var item in list)
            if (item.MediaFileId.HasValue) item.ImageUrl = await media.GetUrlAsync(item.MediaFileId, 400, doFallback: false);
        if (!includePrice || list.Count == 0 ||
            !await PermissionsService.AuthorizeAsync(Permissions.Catalog.DisplayPrice, Customer)) return;
        var ids = list.Select(x => x.Id).ToArray();
        var products = await StorefrontScope.Products(db, Customer, StoreId).Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var options = prices.CreateDefaultOptions(true, Customer);
        foreach (var item in list)
        {
            if (!products.TryGetValue(item.Id, out var product) || product.CallForPrice || product.CustomerEntersPrice) continue;
            var result = await prices.CalculatePriceAsync(new PriceCalculationContext(product, 1, options));
            item.Price = new PriceDto(item.Id, result.FinalPrice.Currency.CurrencyCode, result.FinalPrice.Amount,
                result.RegularPrice?.Amount, result.DiscountAmount.Amount);
        }
    }

    // Native pricing includes applicable discounts for the current customer.
    [HttpGet("products/{id:int}/price"), PublicCatalog]
    [ProducesResponseType(typeof(ApiResult<PriceDto>), 200)]
    public async Task<IActionResult> Price(int id, [FromQuery] int quantity = 1)
    {
        if (quantity < 1 || quantity > 1000) return Error(400, "invalid_quantity");
        if (!await PermissionsService.AuthorizeAsync(Permissions.Catalog.DisplayPrice, Customer))
            return Error(403, "price_access_denied");
        var product = await StorefrontScope.Products(db, Customer, StoreId).FirstOrDefaultAsync(x => x.Id == id);
        if (product == null) return Error(404, "product_not_found");
        if (product.CallForPrice || product.CustomerEntersPrice) return Error(409, "price_unavailable");
        var options = prices.CreateDefaultOptions(false, Customer);
        var p = await prices.CalculatePriceAsync(new PriceCalculationContext(product, quantity, options));
        return Success(new PriceDto(id, p.FinalPrice.Currency.CurrencyCode, p.FinalPrice.Amount, p.RegularPrice?.Amount, p.DiscountAmount.Amount));
    }

    [HttpPost("categories/search"), PublicCatalog]
    [ProducesResponseType(typeof(ApiResult<PagingResult<CategoryDto>>), 200)]
    public async Task<IActionResult> Categories([FromBody] PagingFilter filter)
    {
        if (!StorefrontPaging.Validate<CategoryDto>(filter, "Id", "Name", "ParentId", "DisplayOrder")) return Error(400, "invalid_paging");
        var q = StorefrontScope.Categories(db, Customer, StoreId).AsNoTracking()
            .Select(x => new CategoryDto { Id = x.Id, Name = x.Name, MediaFileId = x.MediaFileId, ParentId = x.ParentId, DisplayOrder = x.DisplayOrder });
        return Success(await q.FilterPaging(filter));
    }

    [HttpPost("brands/search"), PublicCatalog]
    [ProducesResponseType(typeof(ApiResult<PagingResult<BrandDto>>), 200)]
    public async Task<IActionResult> Brands([FromBody] PagingFilter filter)
    {
        if (!StorefrontPaging.Validate<BrandDto>(filter, "Id", "Name")) return Error(400, "invalid_paging");
        var q = StorefrontScope.Brands(db, Customer, StoreId).AsNoTracking()
            .Select(x => new BrandDto { Id = x.Id, Name = x.Name, MediaFileId = x.MediaFileId });
        return Success(await q.FilterPaging(filter));
    }
}
