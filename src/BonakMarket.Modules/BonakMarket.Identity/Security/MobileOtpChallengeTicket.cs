using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using BonakMarket.Core.Messaging;

namespace BonakMarket.Identity.Security;

/// <summary>Opaque, expiring proof of a mobile challenge request. This is not an authentication credential.</summary>
public sealed class MobileOtpChallengeTicket(IDataProtectionProvider protection)
{
    public const int LifetimeSeconds = 300;
    public const int ResendAfterSeconds = 60;
    private readonly ITimeLimitedDataProtector _protector =
        protection.CreateProtector("BonakMarket.Identity.MobileOtpChallenge.v1").ToTimeLimitedDataProtector();

    // Fixed-width generation (zero for suppressed/no active challenge) avoids a response-shape distinction.
    public string Create(string canonicalPhone, long sentTicks, int lifetimeSeconds = LifetimeSeconds) =>
        _protector.Protect(canonicalPhone + "|" + sentTicks.ToString("D19", CultureInfo.InvariantCulture),
            TimeSpan.FromSeconds(lifetimeSeconds + 600)); // Diagnostic grace; SQL enforces the actual OTP expiration.

    public bool TryRead(string ticket, out string phone, out long sentTicks)
    {
        phone = null;
        sentTicks = 0;
        if (string.IsNullOrWhiteSpace(ticket) || ticket.Length > 1024) return false;
        try
        {
            var parts = _protector.Unprotect(ticket).Split('|');
            if (parts.Length != 2 || SmsPhoneNormalizer.Normalize(parts[0]) != parts[0] ||
                !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var generation) ||
                generation <= 0 || generation > DateTime.MaxValue.Ticks)
                return false;
            phone = parts[0];
            sentTicks = generation;
            return true;
        }
        catch (CryptographicException) { return false; }
        catch (FormatException) { return false; }
    }
}
