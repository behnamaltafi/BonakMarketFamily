using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using BonakMarket.Core.Messaging;
using BonakMarket.MultiSms.Configuration;
using BonakMarket.MultiSms.Domain;

namespace BonakMarket.MultiSms.Services;

/// <summary>Short-lived diagnostic codes, isolated from provider credentials and OTP validation.</summary>
public sealed class OtpHistoryProtection(IDataProtectionProvider protection)
{
    private static readonly TimeSpan ActiveOtpLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DiagnosticLifetime = TimeSpan.FromMinutes(15);
    private readonly ITimeLimitedDataProtector _protector =
        protection.CreateProtector("BonakMarket.MultiSms.OtpHistory.v1").ToTimeLimitedDataProtector();

    public string Capture(SmsSendRequest request, SmsGatewaySettings settings)
    {
        if (!settings.HistoryEnabled || !settings.ShowOtpCodeInHistory || request.Kind != SmsMessageKind.Otp ||
            request.VerificationCode is not { Length: 6 } || !request.VerificationCode.All(char.IsAsciiDigit))
            return null;

        return _protector.Protect(request.VerificationCode, DiagnosticLifetime);
    }

    public string Reveal(SmsMessage message, SmsGatewaySettings settings, bool development, DateTime now)
    {
        if (!settings.HistoryEnabled || !settings.ShowOtpCodeInHistory || message.Kind != "Otp" ||
            string.IsNullOrEmpty(message.Body) || now < message.CreatedOnUtc ||
            now >= message.CreatedOnUtc.Add(DiagnosticLifetime) ||
            (!development && now < message.CreatedOnUtc.Add(ActiveOtpLifetime)))
            return null;

        try
        {
            var code = _protector.Unprotect(message.Body);
            return code is { Length: 6 } && code.All(char.IsAsciiDigit) ? code : null;
        }
        catch (CryptographicException) { return null; }
        catch (FormatException) { return null; }
    }
}
