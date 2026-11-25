namespace BonakMarket.Identity.Services;

/// <summary>Known public OTP policy failures, with stable machine-readable codes.</summary>
public sealed class OtpFlowException(string code, int? retryAfterSeconds = null) : Exception(code)
{
    public string Code { get; } = code;
    public int? RetryAfterSeconds { get; } = retryAfterSeconds;
    public int StatusCode => Code == "otp_expired" ? 400 : 429;

    public static int SecondsUntil(DateTime deadline, DateTime now)
        => Math.Max(1, (int)Math.Ceiling((deadline - now).TotalSeconds));
}
