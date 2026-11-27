using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed record DownloadVersionDto(string Version, string FileName, string Changelog);
