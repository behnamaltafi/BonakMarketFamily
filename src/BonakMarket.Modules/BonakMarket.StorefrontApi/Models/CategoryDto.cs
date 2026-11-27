#nullable enable
using System.ComponentModel.DataAnnotations;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Models;

public sealed class CategoryDto
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public int DisplayOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? MediaFileId { get; set; }
    public string? ImageUrl { get; set; }
    public bool HasChildren { get; set; }
    public bool ShowOnHomePage { get; set; }
}
