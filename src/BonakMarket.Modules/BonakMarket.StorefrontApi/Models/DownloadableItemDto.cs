using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record DownloadableItemDto(Guid OrderItemGuid, int OrderId, int ProductId, string ProductName,
    bool DownloadAllowed, bool LicenseAllowed, bool AgreementRequired, int DownloadCount);
