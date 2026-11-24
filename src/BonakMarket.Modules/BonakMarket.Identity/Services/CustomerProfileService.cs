using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Messaging;
using BonakMarket.Identity.Domain;

namespace BonakMarket.Identity.Services;

public sealed class CustomerProfileService : IStorefrontSmsProfileService
{
    private readonly SmartDbContext _db;
    private readonly UserManager<Customer> _userManager;

    public CustomerProfileService(SmartDbContext db, UserManager<Customer> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public Task<string> GetVerifiedPhoneAsync(int customerId, CancellationToken ct = default)
        => _db.Set<SmsLoginPhone>().AsNoTracking().Where(x => x.CustomerId == customerId)
            .Select(x => x.Phone).FirstOrDefaultAsync(ct);

    public async Task<bool> IsProfileIncompleteAsync(int customerId, CancellationToken ct = default)
    {
        var customer = await _db.Customers.IncludeCustomerRoles().FirstOrDefaultAsync(x => x.Id == customerId, ct);
        return await CustomerGuildPolicy.RequiresGuildAsync(_db, customer, ct);
    }

    public async Task<IdentityResult> CompleteProfileAsync(Customer customer, string firstName, string lastName,
        string email, string nationalCode, int? guildId, CancellationToken ct = default)
    {
        if (customer is null || !customer.Active || customer.Deleted || !customer.IsRegistered() || customer.IsSystemAccount)
            return Failed("profile", "account_ineligible");
        var phone = await GetVerifiedPhoneAsync(customer.Id, ct);
        var exempt = CustomerGuildPolicy.IsExempt(customer);
        if (!exempt && (!guildId.HasValue || !await _db.Guilds.AnyAsync(x => x.Id == guildId && x.IsActive, ct)))
            return Failed("guildId", guildId.HasValue ? "guild_invalid" : "guild_required");
        if (guildId.HasValue && !await _db.Guilds.AnyAsync(x => x.Id == guildId && x.IsActive, ct))
            return Failed("guildId", "guild_invalid");
        nationalCode = NationalCodeValidator.Normalize(nationalCode);
        if (!NationalCodeValidator.IsValid(nationalCode)) return Failed("nationalCode", "invalid_national_code");
        firstName = firstName?.Trim();
        lastName = lastName?.Trim();
        email = email?.Trim();
        if (firstName?.Length > 225) return Failed("firstName", "field_too_long");
        if (lastName?.Length > 225) return Failed("lastName", "field_too_long");
        if (email?.Length > 500) return Failed("email", "field_too_long");
        var username = phone != null && !exempt ? SmsPhoneNormalizer.ToProviderPhone(phone) : customer.Username;
        if (username != customer.Username && await _db.Customers.IgnoreQueryFilters().AnyAsync(x => x.Id != customer.Id && x.Username == username, ct))
            return Failed("username", "username_conflict");
        var previous = (customer.Username, customer.Email, customer.FirstName, customer.LastName, customer.FullName, customer.GuildId, customer.NationalCode);
        customer.Username = username;
        customer.Email = string.IsNullOrEmpty(email) ? (phone != null ? "sms-" + phone[1..] + "@accounts.invalid" : customer.Email) : email;
        customer.FirstName = firstName;
        customer.LastName = lastName;
        customer.FullName = (firstName + " " + lastName).Trim();
        customer.GuildId = guildId;
        customer.NationalCode = nationalCode;
        // UserStore persists the tracked Customer and profile marker in one save.
        var binding = await _db.Set<SmsLoginPhone>().FirstOrDefaultAsync(x => x.CustomerId == customer.Id, ct);
        var previousCompletion = binding?.ProfileCompletedOnUtc;
        if (binding != null) binding.ProfileCompletedOnUtc ??= DateTime.UtcNow;
        var result = await _userManager.UpdateAsync(customer);
        if (!result.Succeeded)
        {
            (customer.Username, customer.Email, customer.FirstName, customer.LastName, customer.FullName, customer.GuildId, customer.NationalCode) = previous;
            if (binding != null) binding.ProfileCompletedOnUtc = previousCompletion;
        }
        return result;
    }

    private static IdentityResult Failed(string field, string code) =>
        IdentityResult.Failed(new IdentityError { Code = field, Description = code });
}
