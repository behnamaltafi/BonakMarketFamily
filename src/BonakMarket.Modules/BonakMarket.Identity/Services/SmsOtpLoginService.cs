using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using BonakMarket.Events;
using BonakMarket.Core.Configuration;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Messaging;
using BonakMarket.Identity.Configuration;
using BonakMarket.Identity.Domain;
using BonakMarket.Identity.Security;

namespace BonakMarket.Identity.Services;

/// <summary>
/// Unifies mobile-number login and post-verification Customer registration.
/// Never trusts an unverified Customer.GenericAttributes.Phone value.
/// </summary>
public sealed class SmsOtpLoginService : ISmsOtpLoginService
{
    private readonly SmartDbContext _db;
    private readonly ISmsSender _sms;
    private readonly ISettingFactory _settings;
    private readonly ISmsDeliveryAvailability _delivery;
    private readonly IStorefrontSmsProfileService _profile;
    private readonly IDataProtector _codeProtector;
    private readonly MobileOtpChallengeTicket _mobileTicket;
    private readonly UserManager<Customer> _userManager;
    private readonly SignInManager<Customer> _signInManager;
    private readonly IEventPublisher _events;

    public SmsOtpLoginService(
        SmartDbContext db,
        ISmsSender sms,
        ISettingFactory settings,
        ISmsDeliveryAvailability delivery,
        IStorefrontSmsProfileService profile,
        IDataProtectionProvider protection,
        UserManager<Customer> userManager,
        SignInManager<Customer> signInManager,
        IEventPublisher events)
    {
        _db = db;
        _sms = sms;
        _settings = settings;
        _delivery = delivery;
        _profile = profile;
        _codeProtector = protection.CreateProtector("BonakMarket.MultiSms.Otp.v1");
        _mobileTicket = new MobileOtpChallengeTicket(protection);
        _userManager = userManager;
        _signInManager = signInManager;
        _events = events;
    }

    public async Task<bool> IsEnabledAsync()
    {
        var settings = await _settings.LoadSettingsAsync<CustomerIdentitySettings>();
        return settings.OtpLoginEnabled && await _delivery.IsEnabledAsync();
    }

    public Task<CustomerIdentitySettings> GetOtpSettingsAsync()
        => _settings.LoadSettingsAsync<CustomerIdentitySettings>();

    public Task<string> GetVerifiedPhoneAsync(int customerId, CancellationToken ct = default)
        => _profile.GetVerifiedPhoneAsync(customerId, ct);

    public Task<bool> RequestEnrollmentCodeAsync(Customer customer, string phone, CancellationToken ct = default)
        => RequestPhoneChangeAsync(customer, phone, null, ct);

    public async Task<bool> RequestPhoneChangeAsync(Customer customer, string phone, string proof, CancellationToken ct = default)
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

        var existing = await GetVerifiedPhoneAsync(customer.Id, ct);
        if (existing != null && (existing == canonical || await GetReauthProofAsync(customer.Id, existing, proof, ct) == null))
            return false;
        if (!CustomerGuildPolicy.IsExempt(customer) &&
            await _db.Customers.IgnoreQueryFilters().AnyAsync(x => x.Id != customer.Id && x.Username == SmsPhoneNormalizer.ToProviderPhone(canonical), ct))
            return false;

        if (!await ConsumeEnrollmentQuotaAsync(customer.Id, ct))
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

        var policy = await GetOtpSettingsAsync();
        // Quota applies to every valid phone before checking account eligibility.
        if (!await ConsumeQuotaAsync("login-phone:" + canonical,
            TimeSpan.FromSeconds(policy.EffectiveOtpResendIntervalSeconds()), ct, reportLimits: true))
            throw new OtpFlowException("otp_request_conflict", 1);

        // Same public response for known/unknown numbers. Unknown numbers receive an OTP
        // so they can register only AFTER proving possession of that mobile number.
        var binding = await _db.Set<SmsLoginPhone>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Phone == canonical, ct);
        if (binding is null)
        {
            var customerSettings = await _settings.LoadSettingsAsync<CustomerSettings>();
            if (customerSettings.UserRegistrationType != UserRegistrationType.Standard)
            {
                return;
            }
        }
        else
        {
            var customer = await _db.Customers.IncludeCustomerRoles().FirstOrDefaultAsync(
                x => x.Id == binding.CustomerId, ct);
            if (customer is null || !customer.Active || customer.Deleted || !customer.IsRegistered() ||
                !await _signInManager.CanSignInAsync(customer) ||
                (_userManager.SupportsUserLockout && await _userManager.IsLockedOutAsync(customer)) ||
                (_userManager.SupportsUserTwoFactor && await _userManager.GetTwoFactorEnabledAsync(customer)))
            {
                return;
            }
        }

        await IssueCodeAsync(canonical, "login", ct);
    }

    /// <summary>Reuses the existing per-phone quota and active login challenge, including resend suppression.</summary>
    public async Task<string> RequestMobileChallengeAsync(string canonicalPhone, CancellationToken ct)
    {
        await RequestLoginCodeAsync(canonicalPhone, ct);
        var now = DateTime.UtcNow;
        var policy = await GetOtpSettingsAsync();
        var attempts = policy.EffectiveOtpMaxVerificationAttempts();
        var sent = await _db.Set<SmsOtpChallenge>().AsNoTracking()
            .Where(x => x.Phone == canonicalPhone && x.Purpose == "login" && x.ExpiresOnUtc > now &&
                x.FailedAttempts < attempts)
            .Select(x => (DateTime?)x.LastSentOnUtc).SingleOrDefaultAsync(ct);
        return _mobileTicket.Create(canonicalPhone, sent?.Ticks ?? 0, policy.EffectiveOtpExpirationSeconds());
    }

    public Task<Customer> ConfirmMobileLoginAsync(string ticket, string code, CancellationToken ct)
        => _mobileTicket.TryRead(ticket, out var phone, out var sentTicks)
            ? ConfirmLoginCoreAsync(phone, code, sentTicks, ct)
            : Task.FromResult<Customer>(null);

    public Task<bool> ConfirmEnrollmentAsync(Customer customer, string phone, string code, CancellationToken ct = default)
        => ConfirmPhoneChangeAsync(customer, phone, code, null, ct);

    public async Task<bool> RequestReauthAsync(Customer customer, CancellationToken ct = default)
    {
        if (customer == null || !customer.Active || customer.Deleted || !customer.IsRegistered() || !await IsEnabledAsync()) return false;
        var phone = await GetVerifiedPhoneAsync(customer.Id, ct);
        if (phone == null || !await ConsumeQuotaAsync("reauth-account:" + customer.Id, TimeSpan.FromSeconds((await GetOtpSettingsAsync()).EffectiveOtpResendIntervalSeconds()), ct)) return false;
        return await IssueCodeAsync(phone, "reauth:" + customer.Id, ct);
    }

    public async Task<string> ConfirmReauthAsync(Customer customer, string code, CancellationToken ct = default)
    {
        if (customer == null || !customer.Active || customer.Deleted || !customer.IsRegistered() || !await IsEnabledAsync()) return null;
        var phone = await GetVerifiedPhoneAsync(customer.Id, ct);
        if (phone == null) return null;
        var challenge = await ValidateCodeAsync(phone, "reauth:" + customer.Id, code, ct);
        if (challenge == null) return null;
        var proof = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var now = DateTime.UtcNow;
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var deleted = await _db.Set<SmsOtpChallenge>().Where(x => x.Id == challenge.Id && x.Version == challenge.Version &&
                x.ExpiresOnUtc > now).ExecuteDeleteAsync(ct);
            if (deleted != 1) return null;
            _db.Entry(challenge).State = EntityState.Detached;
            await _db.Set<SmsOtpChallenge>().Where(x => x.Purpose == "reauth-proof:" + customer.Id).ExecuteDeleteAsync(ct);
            _db.Set<SmsOtpChallenge>().Add(new SmsOtpChallenge
            {
                Phone = phone, Purpose = "reauth-proof:" + customer.Id,
                ProtectedCode = _codeProtector.Protect(proof), ExpiresOnUtc = now.AddMinutes(10),
                LastSentOnUtc = now, WindowStartUtc = now, Version = 1
            });
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return proof;
        }
        catch (DbUpdateException) { await transaction.RollbackAsync(ct); _db.ChangeTracker.Clear(); return null; }
    }

    private async Task<SmsOtpChallenge> GetReauthProofAsync(int id, string phone, string proof, CancellationToken ct)
    {
        if (proof is not { Length: 64 }) return null;
        var row = await _db.Set<SmsOtpChallenge>().AsNoTracking().FirstOrDefaultAsync(x =>
            x.Phone == phone && x.Purpose == "reauth-proof:" + id && x.ExpiresOnUtc > DateTime.UtcNow, ct);
        if (row == null) return null;
        try
        {
            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(_codeProtector.Unprotect(row.ProtectedCode)),
                Encoding.UTF8.GetBytes(proof)) ? row : null;
        }
        catch (CryptographicException) { return null; }
    }

    public async Task<bool> ConfirmPhoneChangeAsync(Customer customer, string phone, string code, string proof, CancellationToken ct = default)
    {
        var canonical = SmsPhoneNormalizer.Normalize(phone);
        if (canonical == null || customer == null || !customer.Active || customer.Deleted ||
            !customer.IsRegistered() || !await IsEnabledAsync()) return false;
        var binding = await _db.Set<SmsLoginPhone>().FirstOrDefaultAsync(x => x.CustomerId == customer.Id, ct);
        var old = binding?.Phone;
        var grant = old == null ? null : await GetReauthProofAsync(customer.Id, old, proof, ct);
        if (old != null && (grant == null || old == canonical)) return false;
        var challenge = await ValidateCodeAsync(canonical, "enroll:" + customer.Id, code, ct);
        if (challenge == null) return false;
        var username = SmsPhoneNormalizer.ToProviderPhone(canonical);
        var exempt = CustomerGuildPolicy.IsExempt(customer);
        var placeholder = "sms-" + canonical[1..] + "@accounts.invalid";
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            if (await _db.Set<SmsLoginPhone>().AnyAsync(x => x.Phone == canonical && x.CustomerId != customer.Id, ct) ||
                (!exempt && await _db.Customers.IgnoreQueryFilters().AnyAsync(x => x.Id != customer.Id && x.Username == username, ct)))
                return false;
            // Recheck the old binding under the transaction before consuming proof.
            var current = await _db.Set<SmsLoginPhone>().AsNoTracking().Where(x => x.CustomerId == customer.Id)
                .Select(x => x.Phone).FirstOrDefaultAsync(ct);
            if (current != old) return false;
            var now = DateTime.UtcNow;
            if (grant != null && await _db.Set<SmsOtpChallenge>().Where(x => x.Id == grant.Id && x.Version == grant.Version &&
                x.ExpiresOnUtc > now).ExecuteDeleteAsync(ct) != 1) return false;
            if (await _db.Set<SmsOtpChallenge>().Where(x => x.Id == challenge.Id && x.Version == challenge.Version &&
                x.ExpiresOnUtc > now).ExecuteDeleteAsync(ct) != 1) return false;
            _db.Entry(challenge).State = EntityState.Detached;
            if (binding == null)
            {
                binding = new SmsLoginPhone { CustomerId = customer.Id };
                _db.Set<SmsLoginPhone>().Add(binding);
            }
            binding.Phone = canonical;
            binding.VerifiedOnUtc = now;
            if (!exempt) customer.Username = username;
            if (string.IsNullOrEmpty(customer.Email) || customer.Email.EndsWith("@accounts.invalid", StringComparison.OrdinalIgnoreCase))
                customer.Email = placeholder;
            customer.GenericAttributes.Phone = username;
            // UserManager runs the same native uniqueness checks and the phone username validator.
            var result = await _userManager.UpdateAsync(customer);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync(ct);
                _db.ChangeTracker.Clear();
                return false;
            }
            await _db.Set<MobileApiSession>().Where(x => x.CustomerId == customer.Id && x.RevokedOnUtc == null)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.RevokedOnUtc, now), ct);
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        }
        catch (DbUpdateException) { await transaction.RollbackAsync(ct); _db.ChangeTracker.Clear(); return false; }
    }

    /// <summary>
    /// One OTP works for BOTH sign-in and sign-up. Unbound numbers create a native
    /// Customer after code verification, never before it. Existing accounts are
    /// matched exclusively through the verified phone binding (no untrusted profile-phone lookup).
    /// </summary>
    public Task<Customer> ConfirmLoginAsync(string phone, string code, CancellationToken ct = default)
        => ConfirmLoginCoreAsync(phone, code, null, ct);

    private async Task<Customer> ConfirmLoginCoreAsync(string phone, string code, long? sentTicks, CancellationToken ct)
    {
        var canonical = SmsPhoneNormalizer.Normalize(phone);
        if (canonical is null || !await IsEnabledAsync())
        {
            return null;
        }

        var binding = await _db.Set<SmsLoginPhone>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Phone == canonical, ct);

        Customer customer = null;
        CustomerSettings customerSettings = null;
        CustomerRole registeredRole = null;
        string username = null;
        string internalEmail = null;

        if (binding is not null)
        {
            customer = await _db.Customers.IncludeCustomerRoles()
                .FirstOrDefaultAsync(x => x.Id == binding.CustomerId, ct);
            if (customer is null || customer.Deleted || !customer.Active || !customer.IsRegistered() ||
                !await _signInManager.CanSignInAsync(customer) ||
                (_userManager.SupportsUserLockout && await _userManager.IsLockedOutAsync(customer)) ||
                (_userManager.SupportsUserTwoFactor && await _userManager.GetTwoFactorEnabledAsync(customer)))
            {
                return null;
            }
        }
        else
        {
            customerSettings = await _settings.LoadSettingsAsync<CustomerSettings>();
            // Respect native shop registration policy. Never bypass email validation,
            // disabled registrations or administrator approval through the OTP path.
            if (customerSettings.UserRegistrationType != UserRegistrationType.Standard)
            {
                return null;
            }

            username = SmsPhoneNormalizer.ToProviderPhone(canonical);
            // BonakMarket's native IUserValidator requires Email, even for passwordless
            // users. Reserved .invalid is never a deliverable email address.
            internalEmail = "sms-" + canonical[1..] + "@accounts.invalid";

            // Do not hijack legacy customers merely because their username/phone matches.
            // They must explicitly bind this mobile while signed in to their existing account.
            if (await _db.Customers.IgnoreQueryFilters().AnyAsync(
                x => x.Username == username || x.Email == internalEmail, ct))
            {
                return null;
            }

            registeredRole = await _db.CustomerRoles.FirstOrDefaultAsync(
                x => x.SystemName == SystemCustomerRoleNames.Registered, ct);
            if (registeredRole is null)
            {
                return null;
            }
        }

        var challenge = await ValidateCodeAsync(canonical, "login", code, ct, sentTicks);
        if (challenge is null)
        {
            return null;
        }

        // Consume the code atomically before adding the new Customer and phone binding.
        // The transaction rolls everything back if account creation fails.
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            if (binding != null && !await _db.Set<SmsLoginPhone>().AsNoTracking()
                .AnyAsync(x => x.CustomerId == binding.CustomerId && x.Phone == canonical, ct))
            {
                await transaction.RollbackAsync(ct);
                return null;
            }
            var deleted = await _db.Set<SmsOtpChallenge>()
                .Where(x => x.Id == challenge.Id && x.Version == challenge.Version &&
                    x.ExpiresOnUtc > DateTime.UtcNow)
                .ExecuteDeleteAsync(ct);
            if (deleted != 1)
            {
                await transaction.RollbackAsync(ct);
                return null;
            }

            _db.Entry(challenge).State = EntityState.Detached;
            if (binding is null)
            {
                var now = DateTime.UtcNow;
                customer = new Customer
                {
                    Username = username,
                    Email = internalEmail,
                    Active = true,
                    CreatedOnUtc = now,
                    LastActivityDateUtc = now
                };

                customer.CustomerRoleMappings.Add(new CustomerRoleMapping
                {
                    CustomerRoleId = registeredRole.Id,
                    CustomerRole = registeredRole
                });

                var extraRoleId = customerSettings.RegisterCustomerRoleId ?? 0;
                if (extraRoleId != 0 && extraRoleId != registeredRole.Id)
                {
                    var extraRole = await _db.CustomerRoles.FirstOrDefaultAsync(
                        x => x.Id == extraRoleId && x.Active, ct);
                    if (extraRole is not null)
                    {
                        customer.CustomerRoleMappings.Add(new CustomerRoleMapping
                        {
                            CustomerRoleId = extraRole.Id,
                            CustomerRole = extraRole
                        });
                    }
                }

                var result = await _userManager.CreateAsync(customer);
                if (!result.Succeeded)
                {
                    await transaction.RollbackAsync(ct);
                    return null;
                }

                customer.GenericAttributes.Phone = username;

                _db.Set<SmsLoginPhone>().Add(new SmsLoginPhone
                {
                    CustomerId = customer.Id,
                    Phone = canonical,
                    VerifiedOnUtc = now
                    // ProfileCompletedOnUtc remains null until the new user confirms details.
                });
                await _db.SaveChangesAsync(ct);
            }

            if (binding != null && !CustomerGuildPolicy.IsExempt(customer))
            {
                var expected = SmsPhoneNormalizer.ToProviderPhone(canonical);
                if (customer.Username != expected)
                {
                    if (await _db.Customers.IgnoreQueryFilters().AnyAsync(x => x.Id != customer.Id && x.Username == expected, ct))
                    {
                        await transaction.RollbackAsync(ct);
                        _db.ChangeTracker.Clear();
                        return null;
                    }
                    customer.Username = expected;
                    var update = await _userManager.UpdateAsync(customer);
                    if (!update.Succeeded)
                    {
                        await transaction.RollbackAsync(ct);
                        _db.ChangeTracker.Clear();
                        return null;
                    }
                }
            }
            await transaction.CommitAsync(ct);
            if (binding is null)
            {
                await _events.PublishAsync(new CustomerRegisteredEvent { Customer = customer });
            }
            return customer;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            _db.ChangeTracker.Clear();
            return null;
        }
    }

    public Task<bool> IsProfileIncompleteAsync(int customerId, CancellationToken ct = default)
        => _profile.IsProfileIncompleteAsync(customerId, ct);

    private async Task<bool> ConsumeEnrollmentQuotaAsync(int customerId, CancellationToken ct)
        => await ConsumeQuotaAsync("enroll-account:" + customerId,
            TimeSpan.FromSeconds((await GetOtpSettingsAsync()).EffectiveOtpResendIntervalSeconds()), ct);

    private async Task<bool> ConsumeQuotaAsync(string key, TimeSpan cooldown, CancellationToken ct, bool reportLimits = false)
    {
        var policy = await GetOtpSettingsAsync();
        var now = DateTime.UtcNow;
        var quotas = _db.Set<SmsOtpQuota>();
        var row = await quotas.FirstOrDefaultAsync(x => x.QuotaKey == key, ct);

        if (row is not null)
        {
            if (row.LastSendUtc > now.Subtract(cooldown))
            {
                if (reportLimits) throw new OtpFlowException("otp_resend_too_soon",
                    OtpFlowException.SecondsUntil(row.LastSendUtc.Add(cooldown), now));
                return false;
            }

            if (row.WindowStartUtc <= now.AddDays(-1))
            {
                row.WindowStartUtc = now;
                row.SentCount = 0;
            }

            if (row.SentCount >= policy.EffectiveOtpMaxDailyRequests())
            {
                if (reportLimits) throw new OtpFlowException("otp_daily_limit",
                    OtpFlowException.SecondsUntil(row.WindowStartUtc.AddDays(1), now));
                return false;
            }
        }

        var quota = row ?? new SmsOtpQuota
        {
            QuotaKey = key,
            WindowStartUtc = now
        };

        quota.LastSendUtc = now;
        quota.SentCount++;
        quota.Version++;

        if (row is null)
        {
            quotas.Add(quota);
        }

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

    private async Task<bool> IssueCodeAsync(string phone, string purpose, CancellationToken ct)
    {
        var settings = await GetOtpSettingsAsync();
        var now = DateTime.UtcNow;
        var challenges = _db.Set<SmsOtpChallenge>();
        var current = await challenges.FirstOrDefaultAsync(
            x => x.Phone == phone && x.Purpose == purpose, ct);

        if (current is not null)
        {
            // Login has an independent quota covering consumed challenges.
            if (current.LastSentOnUtc > now.AddSeconds(-settings.EffectiveOtpResendIntervalSeconds()))
            {
                return false;
            }

            if (current.WindowStartUtc <= now.AddDays(-1))
            {
                current.WindowStartUtc = now;
                current.SentInWindow = 0;
            }

            if (current.SentInWindow >= settings.EffectiveOtpMaxDailyRequests())
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
        record.ExpiresOnUtc = now.AddSeconds(settings.EffectiveOtpExpirationSeconds());
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
                settings.FormatOtpMessage(code), SmsMessageKind.Otp) { VerificationCode = code }, ct);
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
        string phone, string purpose, string code, CancellationToken ct, long? sentTicks = null)
    {
        if (code is not { Length: 6 } || !code.All(char.IsAsciiDigit))
        {
            return null;
        }

        var record = await _db.Set<SmsOtpChallenge>().FirstOrDefaultAsync(
            x => x.Phone == phone && x.Purpose == purpose, ct);
        var policy = await GetOtpSettingsAsync();
        if (record is null || string.IsNullOrWhiteSpace(record.ProtectedCode) ||
            (sentTicks.HasValue && record.LastSentOnUtc.Ticks != sentTicks.Value))
            return null;

        if (record.ExpiresOnUtc <= DateTime.UtcNow)
        {
            if (purpose == "login") throw new OtpFlowException("otp_expired");
            return null;
        }

        if (record.FailedAttempts >= policy.EffectiveOtpMaxVerificationAttempts())
        {
            if (purpose == "login") throw new OtpFlowException("otp_attempt_limit");
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
                if (purpose == "login" && record.FailedAttempts >= policy.EffectiveOtpMaxVerificationAttempts())
                    throw new OtpFlowException("otp_attempt_limit");
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
