#nullable enable
namespace BonakMarket.StorefrontApi.Services;

/// <summary>Native download content or an approved HTTPS redirect.</summary>
public sealed record DownloadPayload(string? RedirectUrl = null, Stream? Stream = null, string? MimeType = null, string? FileName = null);
