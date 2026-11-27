#nullable enable
namespace BonakMarket.StorefrontApi.Models;

/// <summary>API capabilities, not administrator permissions. Browser actions require a native browser login.</summary>
public sealed record OrderActionsDto(bool CanReorder, bool CanCancel, bool CanReturn,
    bool CanRetryPayment, bool CanDownloadInvoice, bool RequiresBrowserSession);
