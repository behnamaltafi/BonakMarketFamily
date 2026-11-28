using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Content.Media;
using BonakMarket.Core.Data;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1/downloads")]
[StorefrontPermission(StorefrontPermissions.Downloads)]
[StorefrontPermission(StorefrontPermissions.Orders)]
public sealed class DownloadsController(SmartDbContext db, StorefrontRequestContext request,
    IDownloadService downloads) : StorefrontController
{
    private IQueryable<OrderItem> Owned() => db.OrderItems.Include(x => x.Product).Include(x => x.Order)
        .Where(x => StorefrontScope.Orders(db, request.Customer, request.StoreId).Any(o => o.Id == x.OrderId) && x.Product.IsDownload);

    private DownloadableItemDto Map(OrderItem x) => new(x.OrderItemGuid, x.OrderId, x.ProductId, x.Product.Name,
        downloads.IsDownloadAllowed(x), downloads.IsLicenseDownloadAllowed(x), x.Product.HasUserAgreement, x.DownloadCount);

    [HttpGet]
    public async Task<ActionResult<PageDto<DownloadableItemDto>>> List([FromQuery] DownloadSearchRequest page, CancellationToken ct)
    {
        var query = CommerceListQueries.FilterDownloads(Owned().AsNoTracking(), page)
            .OrderByDescending(x => x.Order.CreatedOnUtc).ThenByDescending(x => x.Id);
        return Ok(await query.PageAsync(page, items => Task.FromResult(items.Select(Map).ToList()), ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DownloadDetailsDto>> Get(Guid id, CancellationToken ct)
    {
        var item = await Owned().AsNoTracking().FirstOrDefaultAsync(x => x.OrderItemGuid == id, ct);
        if (item == null) return Error(404, "download_not_found");
        var dto = Map(item);
        var versions = dto.DownloadAllowed ? (await Versions(item, ct)).Select(x =>
            new DownloadVersionDto(x.FileVersion, x.MediaFile?.Name, x.Changelog)).ToList() : [];
        return Ok(new DownloadDetailsDto(dto, versions, item.Product.HasUserAgreement ? item.Product.UserAgreementText : null));
    }

    // POST because a download consumes a native download allowance.
    [HttpPost("{id:guid}/file")]
    public async Task<IActionResult> FileDownload(Guid id, [FromQuery] bool agree = false, [FromQuery] string version = null, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var item = await Owned().FirstOrDefaultAsync(x => x.OrderItemGuid == id, ct);
        if (item == null) return Error(404, "download_not_found");
        if (!downloads.IsDownloadAllowed(item) || (!item.Product.UnlimitedDownloads && item.DownloadCount >= item.Product.MaxNumberOfDownloads))
            return Error(403, "download_not_allowed");
        if (item.Product.HasUserAgreement && !agree) return Error(422, "agreement_required");
        var file = (await Versions(item, ct)).FirstOrDefault(x => string.IsNullOrEmpty(version) || x.FileVersion == version);
        if (file == null) return Error(404, "file_not_found");
        var result = await Open(file);
        if (result == null) return Error(404, "file_not_found");
        item.DownloadCount++;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    [HttpGet("{id:guid}/license")]
    public async Task<IActionResult> License(Guid id, CancellationToken ct)
    {
        var item = await Owned().AsNoTracking().FirstOrDefaultAsync(x => x.OrderItemGuid == id, ct);
        if (item == null) return Error(404, "download_not_found");
        if (!downloads.IsLicenseDownloadAllowed(item)) return Error(403, "license_not_allowed");
        var file = await db.Downloads.AsNoTracking().Include(x => x.MediaFile)
            .FirstOrDefaultAsync(x => x.Id == item.LicenseDownloadId, ct);
        return file == null ? Error(404, "file_not_found") : await Open(file) ?? Error(404, "file_not_found");
    }

    private async Task<IList<Download>> Versions(OrderItem item, CancellationToken ct) =>
        (await db.Downloads.AsNoTracking().ApplyEntityFilter(item.Product).Include(x => x.MediaFile).ToListAsync(ct)).OrderByVersion();

    private async Task<IActionResult> Open(Download file)
    {
        if (file.UseDownloadUrl)
        {
            if (Uri.TryCreate(file.DownloadUrl, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps)
                return Redirect(file.DownloadUrl);
            return null;
        }
        if (file.MediaFile == null || file.MediaFile.Size == 0) return null;
        var stream = await downloads.OpenDownloadStreamAsync(file);
        if (stream == null) return null;
        if (stream.CanSeek && stream.Length == 0) { await stream.DisposeAsync(); return null; }
        return new FileStreamResult(stream, file.MediaFile.MimeType) { FileDownloadName = file.MediaFile.Name };
    }
}
