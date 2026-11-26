using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BonakMarket.Core.Messaging;

/// <summary>A logical OTP template and its configured delivery readiness. No vendor credentials or mappings are exposed.</summary>
public sealed record SmsOtpTemplateOption(string Key, string Title, bool IsReady);

/// <summary>Reports transport availability independently of customer login policy.</summary>
public interface ISmsDeliveryAvailability
{
    Task<bool> IsEnabledAsync();

    // Fail closed for transports that do not implement approved OTP template support.
    Task<bool> IsOtpTemplateAvailableAsync(string templateKey, CancellationToken ct = default)
        => Task.FromResult(false);

    Task<IReadOnlyList<SmsOtpTemplateOption>> GetOtpTemplatesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SmsOtpTemplateOption>>(Array.Empty<SmsOtpTemplateOption>());
}
