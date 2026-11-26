namespace BonakMarket.MultiSms.Services;

public static class SmsHistoryFormatter
{
    public static string MaskPhone(string phone)
    {
        if (string.IsNullOrEmpty(phone)) return "";
        return phone.Length > 7
            ? phone[..4] + new string('*', phone.Length - 7) + phone[^3..]
            : "***";
    }

    public static string SafeCategory(Exception exception) => exception switch
    {
        OperationCanceledException => "Cancelled",
        TimeoutException => "Timeout",
        HttpRequestException => "Transport",
        SmsProviderException ex when ex.SafeToFailover => "ProviderRejected",
        SmsProviderException => "ProviderUnconfirmed",
        InvalidOperationException => "Configuration",
        ArgumentException => "InvalidInput",
        _ => "Unknown"
    };

    // This diagnostic fits the existing 60-character ErrorCategory column. No raw
    // exception messages, URLs, credentials, recipient or SMS content are logged.
    public static string SafeDiagnostic(Exception exception)
    {
        var category = SafeCategory(exception);
        if (exception is not SmsProviderException provider) return category;

        var result = category;
        if (provider.HttpStatusCode is >= 100 and <= 599)
            result += FormattableString.Invariant($"; HTTP={provider.HttpStatusCode}");
        if (provider.ProviderStatus.HasValue)
            result += FormattableString.Invariant($"; Status={provider.ProviderStatus}");
        if (provider.ProviderErrorCode is >= -9999 and <= 9999)
            result += FormattableString.Invariant($"; Code={provider.ProviderErrorCode}");
        if (provider.Reason != SmsFailureReason.None && Enum.IsDefined(provider.Reason))
        {
            var reason = "; " + provider.Reason;
            if (result.Length + reason.Length <= 60) result += reason;
        }
        return result;
    }

    public static string FailedStatus(Exception exception)
        => exception is SmsProviderException { SafeToFailover: true } ? "Rejected" : "Unknown";
}
