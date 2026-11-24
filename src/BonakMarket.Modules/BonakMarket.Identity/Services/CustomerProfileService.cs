using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Configuration;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Messaging;
using BonakMarket.Identity.Configuration;
using BonakMarket.Identity.Domain;

namespace BonakMarket.Identity.Services;

/// <summary>Owns profile data and phone-first onboarding, not OTP issuance or verification.</summary>
public sealed class CustomerProfileService : IStorefrontSmsProfileService
{
    private readonly SmartDbContext _db;
    private readonly UserManager<Customer> _userManager;
    private readonly ISettingFactory _settings;
    private readonly ISmsDeliveryAvailability _delivery;

    public CustomerProfileService(SmartDbContext db, UserManager<Customer> userManager,
        ISettingFactory settings, ISmsDeliveryAvailability delivery)
    {
        _db = db;
        _userManager = userManager;
        _settings = settings;
        _delivery = delivery;
    }

    public async Task<bool> IsPhoneProfileEnabledAsync()
    {
        var settings = await _settings.LoadSettingsAsync<CustomerIdentitySettings>();
        return settings.OtpLoginEnabled && await _delivery.IsEnabledAsync();
    }

    public async Task<string> GetVerifiedPhoneAsync(int customerId, CancellationToken ct = default)
        => await _db.Set<SmsLoginPhone>().AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .Select(x => x.Phone)
            .FirstOrDefaultAsync(ct);

    public Task<bool> IsProfileIncompleteAsync(int customerId, CancellationToken ct = default)
        => _db.Set<SmsLoginPhone>().AsNoTracking()
            .AnyAsync(x => x.CustomerId == customerId && x.ProfileCompletedOnUtc == null, ct);

    private async Task<bool> MarkProfileCompletedAsync(int customerId, CancellationToken ct = default)
    {
        var row = await _db.Set<SmsLoginPhone>().FirstOrDefaultAsync(
            x => x.CustomerId == customerId, ct);
        if (row is null)
        {
            return false;
        }

        row.ProfileCompletedOnUtc ??= DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Keep mobile identity independent from the public username. This also works
    /// for later profile edits, and never accepts a client-supplied customer ID.
    /// A blank email is allowed for phone-first users; BonakMarket's core validator
    /// requires the reserved internal placeholder to remain in the database.
    /// </summary>
    public async Task<IdentityResult> CompleteProfileAsync(
        Customer customer, string username, string firstName, string lastName, string email,
        CancellationToken ct = default)
    {
        if (customer is null || !customer.Active || customer.Deleted || !customer.IsRegistered() ||
            !await IsPhoneProfileEnabledAsync())
        {
            return IdentityResult.Failed(new IdentityError { Description = "The account is not eligible." });
        }

        var phone = await GetVerifiedPhoneAsync(customer.Id, ct);
        if (phone is null)
        {
            return IdentityResult.Failed(new IdentityError { Description = "A verified phone number is required." });
        }

        username = username?.Trim();
        if (string.IsNullOrEmpty(username) || username.Length > 100)
        {
            return IdentityResult.Failed(new IdentityError { Description = "Enter a valid username." });
        }

        firstName = firstName?.Trim();
        lastName = lastName?.Trim();
        email = email?.Trim();
        if (firstName?.Length > 225 || lastName?.Length > 225 || email?.Length > 500)
        {
            return IdentityResult.Failed(new IdentityError { Description = "A profile field is too long." });
        }

        var previous = (customer.Username, customer.Email, customer.FirstName,
            customer.LastName, customer.FullName);
        customer.Username = username;
        customer.Email = string.IsNullOrEmpty(email) ?
            "sms-" + phone[1..] + "@accounts.invalid" : email;
        customer.FirstName = firstName;
        customer.LastName = lastName;
        customer.FullName = (firstName + " " + lastName).Trim();

        var result = await _userManager.UpdateAsync(customer);
        if (!result.Succeeded)
        {
            (customer.Username, customer.Email, customer.FirstName,
                customer.LastName, customer.FullName) = previous;
            return result;
        }

        await MarkProfileCompletedAsync(customer.Id, ct);
        return result;
    }

}
