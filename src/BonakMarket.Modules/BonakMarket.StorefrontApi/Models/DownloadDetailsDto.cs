using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record DownloadDetailsDto(DownloadableItemDto Item, List<DownloadVersionDto> Versions, string AgreementText);
