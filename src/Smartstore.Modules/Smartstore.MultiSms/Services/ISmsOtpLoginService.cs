#nullable enable

using Smartstore.Core.Identity;

namespace Smartstore.MultiSms.Services;

/// <summary>
/// Shared OTP operations for storefront forms and the JSON API.
/// </summary>
public interface ISmsOtpLoginService
{
    /// <summary>Checks whether SMS and OTP login are enabled.</summary>
    Task<bool> IsEnabledAsync();

    /// <summary>Gets the authenticated account's verified mobile number.</summary>
    Task<string?> GetVerifiedPhoneAsync(int customerId, CancellationToken ct = default);

    /// <summary>Requests verification of a mobile number for an existing account.</summary>
    Task<bool> RequestEnrollmentCodeAsync(Customer customer, string phone, CancellationToken ct = default);

    /// <summary>Requests a login code without disclosing whether the number is registered.</summary>
    Task RequestLoginCodeAsync(string phone, CancellationToken ct = default);

    /// <summary>Verifies and binds a mobile number to its authenticated account.</summary>
    Task<bool> ConfirmEnrollmentAsync(Customer customer, string phone, string code, CancellationToken ct = default);

    /// <summary>Consumes a login code and returns its account, or null on failure.</summary>
    Task<Customer?> ConfirmLoginAsync(string phone, string code, CancellationToken ct = default);
}
