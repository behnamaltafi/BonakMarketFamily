using Microsoft.AspNetCore.Mvc;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1/downloads")]
[StorefrontPermission(StorefrontPermissions.Downloads)]
[StorefrontPermission(StorefrontPermissions.Orders)]
public sealed class DownloadsController(DownloadStorefrontService downloads) : StorefrontController
{
    [HttpGet]
    public async Task<ActionResult<PageDto<DownloadableItemDto>>> List([FromQuery] PageRequest page, CancellationToken ct) =>
        Ok(await downloads.ListAsync(page, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DownloadDetailsDto>> Get(Guid id, CancellationToken ct) =>
        Respond(await downloads.GetAsync(id, ct));

    // POST because a download consumes a native download allowance.
    [HttpPost("{id:guid}/file")]
    public async Task<IActionResult> FileDownload(Guid id, [FromQuery] bool agree = false, [FromQuery] string version = null, CancellationToken ct = default) =>
        DownloadResponse(await downloads.FileDownloadAsync(id, agree, version, ct));

    [HttpGet("{id:guid}/license")]
    public async Task<IActionResult> License(Guid id, CancellationToken ct) =>
        DownloadResponse(await downloads.LicenseAsync(id, ct));

    private IActionResult DownloadResponse(StorefrontResult<DownloadPayload> result)
    {
        if (result.Failure != null)
            return FailureResponse(result.Failure);
        var payload = result.Value;
        return payload.RedirectUrl != null
            ? Redirect(payload.RedirectUrl)
            : new FileStreamResult(payload.Stream, payload.MimeType) { FileDownloadName = payload.FileName };
    }
}
