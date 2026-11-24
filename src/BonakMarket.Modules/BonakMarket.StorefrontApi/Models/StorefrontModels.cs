using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string ShortDescription { get; set; }
    public string Sku { get; set; }
    public int? MediaFileId { get; set; }
    public string ImageUrl { get; set; }
    public PriceDto Price { get; set; }
}
public sealed class CategoryDto
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public int DisplayOrder { get; set; }
    public string Name { get; set; }
    public int? MediaFileId { get; set; }
}
public sealed class BrandDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int? MediaFileId { get; set; }
}
public sealed class OrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public decimal OrderTotal { get; set; }
    public int OrderStatusId { get; set; }
    public int PaymentStatusId { get; set; }
    public int ShippingStatusId { get; set; }
}
public sealed record OrderLineDto(int Id, int ProductId, int Quantity, decimal UnitPriceInclTax, decimal PriceInclTax);
public sealed record OrderDetailDto(OrderDto Order, List<OrderLineDto> Items);
public sealed class CouponRequest
{
    [Required, StringLength(200)] public string Code { get; set; }
}
public sealed record CartTotalDto(decimal? Total, decimal DiscountAmount, string CurrencyCode);
public sealed record CartItemDto(int Id, int ProductId, int Quantity, bool Active, int? ParentItemId);
public sealed record PriceDto(int ProductId, string CurrencyCode, decimal FinalPrice, decimal? RegularPrice, decimal DiscountAmount);
public sealed class AddCartItem
{
    [Range(1, int.MaxValue)] public int ProductId { get; set; }
    [Range(1, 1000)] public int Quantity { get; set; } = 1;
    [MaxLength(30)] public List<VariantChoice> Variants { get; set; } = new();
}
public sealed class UpdateCartItem
{
    [Range(1, 1000)] public int Quantity { get; set; }
}

public record PageDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages)
{
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
public class PageRequest
{
    [Range(1, 10000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}
public enum CatalogSort { Initial, NameAsc, NameDesc, PriceAsc, PriceDesc, Newest }
public sealed class ProductSearchRequest : PageRequest, IValidatableObject
{
    [StringLength(200)] public string Search { get; set; }
    [Range(1, int.MaxValue)] public int? CategoryId { get; set; }
    [Range(1, int.MaxValue)] public int? BrandId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool? InStock { get; set; }
    public bool? HasDiscount { get; set; }
    public CatalogSort Sort { get; set; }
    public bool IncludePrice { get; set; } = true;
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!Enum.IsDefined(Sort)) yield return new ValidationResult("Invalid sort.", new[] { nameof(Sort) });
        if (MinPrice < 0 || MaxPrice < 0 || MinPrice > MaxPrice)
            yield return new ValidationResult("Invalid price range.", new[] { nameof(MinPrice), nameof(MaxPrice) });
    }
}
public sealed class CategorySearchRequest : PageRequest
{
    [StringLength(200)] public string Search { get; set; }
    [Range(0, int.MaxValue)] public int? ParentId { get; set; }
}
public sealed class BrandSearchRequest : PageRequest
{
    [StringLength(200)] public string Search { get; set; }
}
public sealed class OrderSearchRequest : PageRequest
{
    public int? OrderStatusId { get; set; }
    public int? PaymentStatusId { get; set; }
    public int? ShippingStatusId { get; set; }
}

public sealed class VariantChoice
{
    [Range(1, int.MaxValue)] public int VariantAttributeId { get; set; }
    [Range(1, int.MaxValue)] public int ValueId { get; set; }
}
public sealed record VariantValueDto(int Id, string Name);
public sealed record VariantDto(int Id, string Name, int ControlTypeId, bool Required, List<VariantValueDto> Values);
