using BonakMarket.MultiSms.Configuration;

namespace BonakMarket.MultiSms.Services;

public interface ISmsProvider
{
    string Name { get; }
    string GetMethod(bool templateMode, SmsGatewaySettings settings) => templateMode ? "Template" : "FreeText";
    bool IsAvailable(SmsGatewaySettings settings);
    Task<string> SendAsync(string phone, string text, SmsGatewaySettings settings, CancellationToken cancelToken);
}

/// <summary>Provider capability for any approved service template, independent of message purpose.</summary>
public interface ITemplateSmsProvider
{
    bool IsTemplateAvailable(SmsGatewaySettings settings);
    bool IsValidMapping(string externalId, IEnumerable<string> slots);
    Task<string> SendTemplateAsync(string phone, SmsTemplatePayload template, SmsGatewaySettings settings, CancellationToken ct);
}

/// <summary>
/// Only explicit provider rejection is safe for automatic failover.
/// Timeouts, connection resets and malformed responses are ambiguous: the SMS may already exist.
/// </summary>
public sealed class SmsProviderException : Exception
{
    public SmsProviderException(string message, bool safeToFailover = false,
        int? httpStatusCode = null, int? providerStatus = null, int? providerErrorCode = null,
        SmsFailureReason reason = SmsFailureReason.None)
        : base(message)
    {
        SafeToFailover = safeToFailover;
        HttpStatusCode = httpStatusCode;
        ProviderStatus = providerStatus;
        ProviderErrorCode = providerErrorCode;
        Reason = reason;
    }

    public bool SafeToFailover { get; }
    public int? HttpStatusCode { get; }
    public int? ProviderStatus { get; }
    public int? ProviderErrorCode { get; }
    public SmsFailureReason Reason { get; }
}

// Fixed diagnostics only. Never persist arbitrary provider response text.
public enum SmsFailureReason
{
    None,
    ProviderUnavailable,
    CredentialsMissing,
    CredentialsNotEncrypted,
    CredentialsDecryptionFailed,
    MissingConfirmation,
    InvalidResponse,
    IncompleteConfirmation
}
