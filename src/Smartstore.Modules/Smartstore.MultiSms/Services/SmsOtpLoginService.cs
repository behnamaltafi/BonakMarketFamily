using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Smartstore.Core.Configuration;
using Smartstore.Core.Data;
using Smartstore.Core.Identity;
using Smartstore.Core.Messaging;
using Smartstore.MultiSms.Configuration;
using Smartstore.MultiSms.Domain;

namespace Smartstore.MultiSms.Services;

/// <summary>
/// Enrolls a verified phone for an existing native Customer and issues
/// single-use login codes. It never authenticates against an unverified
/// Customer.GenericAttributes.Phone value.
/// </summary>
public sealed class SmsOtpLoginService
{
    private readonly SmartDbContext _db;
    private readonly ISmsSender _sms;
    private readonly ISettingFactory _settings;
    private readonly IDataProtector _codeProtector;

    public SmsOtpLoginService(
        SmartDbContext db,
        ISmsSender sms,
        ISettingFactory settings,
        IDataProtectionProvider protection)
    {
        _db = db;
        _sms = sms;
        _settings = settings;
        _codeProtector = protection.CreateProtector("Smartstore.MultiSms.Otp.v1");
    }

    public async Task<bool> IsEnabledAsync()
    {
        var settings = await _settings.LoadSettingsAsync<SmsGatewaySettings>();
        return settings.Enabled && settings.OtpLoginEnabled;
    }

    public async Task<string> GetVerifiedPhoneAsync(int customerId, CancellationToken ct = default)
        => await _db.Set<SmsLoginPhone>().AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .Select(x => x.Phone)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> RequestEnrollmentCodeAsync(Customer customer, string phone, CancellationToken ct = default)
    {
        var canonical = SmsPhoneNormalizer.Normalize(phone);
        if (canonical is null || customer is null || !customer.Active || customer.Deleted ||
            !customer.IsRegistered() || !await IsEnabledAsync())
        {
            return false;
        }

        if (await _db.Set<SmsLoginPhone>().AsNoTracking().AnyAsync(
            x => x.Phone == canonical && x.CustomerId != customer.Id, ct))
        {
            return false;
        }

        return await IssueCodeAsync(canonical, "enroll:" + customer.Id, ct);
    }

    public async Task RequestLoginCodeAsync(string phone, CancellationToken ct = default)
    {
        var canonical = SmsPhoneNormalizer.Normalize(phone);
        if (canonical is null || !await IsEnabledAsync())
        {
            return;
        }

        // Unknown numbers intentionally return exactly the same public result.
        // Only verified mobile identities are eligible to receive a login code.
        var binding = await _db.Set<SmsLoginPhone>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Phone == canonical, ct);
        if (binding is null)
        {
            return;
        }

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == binding.CustomerId, ct);
        if (customer is null || !customer.Active || customer.Deleted)
        {
            return;
        }

        await IssueCodeAsync(canonical, "login", ct);
    }

    public async Task<bool> ConfirmEnrollmentAsync(Customer customer, string phone, string code,
        CancellationToken ct = default)
    {
        var canonical = SmsPhoneNormalizer.Normalize(phone);
        if (canonical is null || customer is null || !customer.Active || customer.Deleted ||
            !customer.IsRegistered() || !await IsEnabledAsync())
        {
            return false;
        }

        var challenge = await ValidateCodeAsync(canonical, "enroll:" + customer.Id, code, ct);
        if (challenge is null)
        {
            return false;
        }

        if (await _db.Set<SmsLoginPhone>().AsNoTracking().AnyAsync(
            x => x.Phone == canonical && x.CustomerId != customer.Id, ct))
        {
            return false;
        }

        var binding = await _db.Set<SmsLoginPhone>().FirstOrDefaultAsync(
            x => x.CustomerId == customer.Id, ct);
        if (binding is null)
        {
            _db.Set<SmsLoginPhone>().Add(new SmsLoginPhone
            {
                CustomerId = customer.Id,
                Phone = canonical,
                VerifiedOnUtc = DateTime.UtcNow
            });
        }
        else
        {
            binding.Phone = canonical;
            binding.VerifiedOnUtc = DateTime.UtcNow;
        }

        _db.Set<SmsOtpChallenge>().Remove(challenge);
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task<Customer> ConfirmLoginAsync(string phone, string code,
        CancellationToken ct = default)
    {
        var canonical = SmsPhoneNormalizer.Normalize(phone);
        if (canonical is null || !await IsEnabledAsync())
        {
            return null;
        }

        var binding = await _db.Set<SmsLoginPhone>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Phone == canonical, ct);
        if (binding is null)
        {
            return null;
        }

        var challenge = await ValidateCodeAsync(canonical, "login", code, ct);
        if (challenge is null)
        {
            return null;
        }

        var customer = await _db.Customers
            .FirstOrDefaultAsync(x => x.Id == binding.CustomerId, ct);
        if (customer is null || !customer.Active || customer.Deleted ||
            !customer.IsRegistered())
        {
            return null;
        }

        _db.Set<SmsOtpChallenge>().Remove(challenge);
        try
        {
            await _db.SaveChangesAsync(ct);
            return customer;
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            return null;
        }
    }

    private async Task<bool> IssueCodeAsync(string phone, string purpose, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var challenges = _db.Set<SmsOtpChallenge>();
        var current = await challenges.FirstOrDefaultAsync(
            x => x.Phone == phone && x.Purpose == purpose, ct);

        if (current is not null)
        {
            // Two minutes between sends, maximum 10 sends in a rolling 24h.
            if (current.LastSentOnUtc > now.AddMinutes(-2))
            {
                return false;
            }

            if (current.WindowStartUtc <= now.AddDays(-1))
            {
                current.WindowStartUtc = now;
                current.SentInWindow = 0;
            }

            if (current.SentInWindow >= 10)
            {
                return false;
            }
        }

        string code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var record = current ?? new SmsOtpChallenge
        {
            Phone = phone,
            Purpose = purpose,
            WindowStartUtc = now
        };

        record.ProtectedCode = _codeProtector.Protect(code);
        record.ExpiresOnUtc = now.AddMinutes(5);
        record.LastSentOnUtc = now;
        record.FailedAttempts = 0;
        record.SentInWindow++;
        record.Version++;

        if (current is null)
        {
            challenges.Add(record);
        }

        // The concurrency token and unique (phone,purpose) index stop parallel
        // requests from issuing several valid codes for the same destination.
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            return false;
        }

        try
        {
            await _sms.SendAsync(new SmsSendRequest(phone,
                $"Your Smartstore verification code is {code}. Valid for 5 minutes."), ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never automatically retry an unknown provider outcome: duplicate
            // authentication SMS may be sent. A failed send retains its cooldown.
            return false;
        }
    }

    private async Task<SmsOtpChallenge> ValidateCodeAsync(
        string phone, string purpose, string code, CancellationToken ct)
    {
        if (code is not { Length: 6 } || !code.All(char.IsAsciiDigit))
        {
            return null;
        }

        var record = await _db.Set<SmsOtpChallenge>().FirstOrDefaultAsync(
            x => x.Phone == phone && x.Purpose == purpose, ct);
        if (record is null || record.ExpiresOnUtc <= DateTime.UtcNow ||
            record.FailedAttempts >= 5 || string.IsNullOrWhiteSpace(record.ProtectedCode))
        {
            return null;
        }

        bool valid = false;
        try
        {
            var expected = _codeProtector.Unprotect(record.ProtectedCode);
            valid = CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(code));
        }
        catch (CryptographicException)
        {
            // A rotated/lost key ring fails closed.
        }

        if (!valid)
        {
            record.FailedAttempts++;
            record.Version++;
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                _db.ChangeTracker.Clear();
            }
            return null;
        }

        return record;
    }
}
