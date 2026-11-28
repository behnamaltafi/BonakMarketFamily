using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Content.Media;
using BonakMarket.Core.Data;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Services;

public sealed class DownloadStorefrontService(SmartDbContext db, StorefrontRequestContext request,
    IDownloadService downloads)
{
    private IQueryable<OrderItem> Owned() => db.OrderItems.Include(x => x.Product).Include(x => x.Order)
        .Where(x => StorefrontScope.Orders(db, request.Customer, request.StoreId).Any(o => o.Id == x.OrderId) && x.Product.IsDownload);

    private DownloadableItemDto Map(OrderItem x) => new(x.OrderItemGuid, x.OrderId, x.ProductId, x.Product.Name,
        downloads.IsDownloadAllowed(x), downloads.IsLicenseDownloadAllowed(x), x.Product.HasUserAgreement, x.DownloadCount);

    public async Task<PageDto<DownloadableItemDto>> ListAsync(PageRequest page, CancellationToken ct)
    {
        var query = Owned().AsNoTracking().OrderByDescending(x => x.Order.CreatedOnUtc).ThenByDescending(x => x.Id);
        return await query.PageAsync(page, items => Task.FromResult(items.Select(Map).ToList()), ct);
    }

    public async Task<StorefrontResult<DownloadDetailsDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var item = await Owned().AsNoTracking().FirstOrDefaultAsync(x => x.OrderItemGuid == id, ct);
        if (item == null) return new StorefrontFailure(404, "download_not_found");
        var dto = Map(item);
        var versions = dto.DownloadAllowed ? (await Versions(item, ct)).Select(x =>
            new DownloadVersionDto(x.FileVersion, x.MediaFile?.Name, x.Changelog)).ToList() : [];
        return new(new DownloadDetailsDto(dto, versions, item.Product.HasUserAgreement ? item.Product.UserAgreementText : null));
    }

    // POST because a download consumes a native download allowance.
    public async Task<StorefrontResult<DownloadPayload>> FileDownloadAsync(Guid id, bool agree, string version, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var item = await Owned().FirstOrDefaultAsync(x => x.OrderItemGuid == id, ct);
        if (item == null) return new StorefrontFailure(404, "download_not_found");
        if (!downloads.IsDownloadAllowed(item) || (!item.Product.UnlimitedDownloads && item.DownloadCount >= item.Product.MaxNumberOfDownloads))
            return new StorefrontFailure(403, "download_not_allowed");
        if (item.Product.HasUserAgreement && !agree) return new StorefrontFailure(422, "agreement_required");
        var file = (await Versions(item, ct)).FirstOrDefault(x => string.IsNullOrEmpty(version) || x.FileVersion == version);
        if (file == null) return new StorefrontFailure(404, "file_not_found");
        var result = await Open(file);
        if (result == null) return new StorefrontFailure(404, "file_not_found");
        item.DownloadCount++;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(result);
    }

    public async Task<StorefrontResult<DownloadPayload>> LicenseAsync(Guid id, CancellationToken ct)
    {
        var item = await Owned().AsNoTracking().FirstOrDefaultAsync(x => x.OrderItemGuid == id, ct);
        if (item == null) return new StorefrontFailure(404, "download_not_found");
        if (!downloads.IsLicenseDownloadAllowed(item)) return new StorefrontFailure(403, "license_not_allowed");
        var file = await db.Downloads.AsNoTracking().Include(x => x.MediaFile)
            .FirstOrDefaultAsync(x => x.Id == item.LicenseDownloadId, ct);
        var result = file == null ? null : await Open(file);
        return result == null
            ? new StorefrontResult<DownloadPayload>(null, new StorefrontFailure(404, "file_not_found"))
            : new(result);
    }

    private async Task<IList<Download>> Versions(OrderItem item, CancellationToken ct) =>
        (await db.Downloads.AsNoTracking().ApplyEntityFilter(item.Product).Include(x => x.MediaFile).ToListAsync(ct)).OrderByVersion();

    private async Task<DownloadPayload> Open(Download file)
    {
        if (file.UseDownloadUrl)
        {
            if (Uri.TryCreate(file.DownloadUrl, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps)
                return new DownloadPayload(file.DownloadUrl);
            return null;
        }
        if (file.MediaFile == null || file.MediaFile.Size == 0) return null;
        var stream = await downloads.OpenDownloadStreamAsync(file);
        if (stream == null) return null;
        if (stream.CanSeek && stream.Length == 0) { await stream.DisposeAsync(); return null; }
        // FileStreamResult used to validate the MIME type before the download allowance was committed.
        // Keep validation on this side of the transaction even though MVC constructs the result later.
        var contentType = MediaTypeHeaderValue.Parse(file.MediaFile.MimeType).ToString();
        return new DownloadPayload(Stream: stream, MimeType: contentType, FileName: file.MediaFile.Name);
    }
}
