using System.ComponentModel.DataAnnotations;

namespace BonakMarket.StorefrontApi.Models;

public sealed class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string ShortDescription { get; set; }
}
public sealed class CategoryDto
{
    public int Id { get; set; }
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
public sealed record ProfileDto(int Id, string Username, string FirstName, string LastName, string Email);
public sealed record CartItemDto(int Id, int ProductId, int Quantity, bool Active, int? ParentItemId);
public sealed record PriceDto(int ProductId, string CurrencyCode, decimal FinalPrice, decimal? RegularPrice, decimal DiscountAmount);
public sealed class AddCartItem
{
    [Range(1, int.MaxValue)] public int ProductId { get; set; }
    [Range(1, 1000)] public int Quantity { get; set; } = 1;
}
public sealed class UpdateCartItem
{
    [Range(1, 1000)] public int Quantity { get; set; }
}
