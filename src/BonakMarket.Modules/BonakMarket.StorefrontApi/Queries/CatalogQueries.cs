using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Queries;

public sealed class CatalogQueries(SmartDbContext db, StorefrontRequestContext request)
{
    public Task<PageDto<CategoryDto>> CategoriesAsync(CategorySearchRequest filter, CancellationToken ct)
    {
        var query = StorefrontScope.Categories(db, request.Customer, request.StoreId).AsNoTracking();
        if (filter.ParentId.HasValue)
            query = filter.ParentId == 0 ? query.Where(x => x.ParentId == null) : query.Where(x => x.ParentId == filter.ParentId);
        if (!string.IsNullOrWhiteSpace(filter.Search)) query = query.Where(x => x.Name.Contains(filter.Search));
        return query.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)
            .Select(x => new CategoryDto { Id = x.Id, ParentId = x.ParentId, DisplayOrder = x.DisplayOrder,
                Name = x.Name, MediaFileId = x.MediaFileId }).PageAsync(filter, ct);
    }

    public Task<PageDto<BrandDto>> BrandsAsync(BrandSearchRequest filter, CancellationToken ct)
    {
        var query = StorefrontScope.Brands(db, request.Customer, request.StoreId).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search)) query = query.Where(x => x.Name.Contains(filter.Search));
        return query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new BrandDto { Id = x.Id, Name = x.Name, MediaFileId = x.MediaFileId }).PageAsync(filter, ct);
    }
}
