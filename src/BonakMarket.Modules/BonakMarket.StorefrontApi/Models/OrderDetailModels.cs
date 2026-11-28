#nullable enable
namespace BonakMarket.StorefrontApi.Models;

public sealed record OrderStatusDto(int Id, string Code, string Title);
public sealed record OrderProductPreviewDto(int ProductId, string? Name, string? ImageUrl);

/// <summary>Stored native monetary amounts in order base currency, not today's customer currency.</summary>
public sealed record OrderFinancialsDto(decimal SubtotalInclTax, decimal SubtotalExclTax,
    decimal SubtotalDiscountInclTax, decimal SubtotalDiscountExclTax,
    decimal ShippingInclTax, decimal ShippingExclTax, decimal PaymentFeeInclTax, decimal PaymentFeeExclTax,
    decimal Tax, decimal OrderDiscount, decimal CreditBalance, decimal GiftCardAmount,
    decimal Rounding, decimal Total, decimal RefundedAmount, string? CustomerCurrencyCode, decimal CurrencyRate);

public sealed record OrderAddressDto(int Id, string? FirstName, string? LastName, string? Company,
    string? Country, string? StateProvince, string? City, string? Address1, string? Address2,
    string? ZipPostalCode, string? PhoneNumber, string? Email,
    int? CityId = null, decimal? Latitude = null, decimal? Longitude = null);

/// <summary>API capabilities, not administrator permissions. Browser actions require a native browser login.</summary>
public sealed record OrderActionsDto(bool CanReorder, bool CanCancel, bool CanReturn,
    bool CanRetryPayment, bool CanDownloadInvoice, bool RequiresBrowserSession);

public sealed record OrderPaymentStatusDto(int OrderId, OrderStatusDto OrderStatus, OrderStatusDto PaymentStatus,
    bool IsPaid, DateTime? PaidDateUtc, DateTime UpdatedOnUtc, decimal OrderTotal, decimal RefundedAmount,
    string? CustomerCurrencyCode, decimal CurrencyRate, bool CanRetryPayment,
    bool RequiresBrowserSession, string? BrowserPaymentUrl);
