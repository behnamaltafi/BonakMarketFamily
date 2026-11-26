using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Messaging;
using BonakMarket.Identity.Domain;

namespace BonakMarket.Identity.Security;

// Registered alongside the native validator: MVC, admin and OData UserManager paths cannot change a bound username.
public sealed class PhoneUsernameValidator(Lazy<SmartDbContext> db) : IUserValidator<Customer>
{
    public async Task<IdentityResult> ValidateAsync(UserManager<Customer> manager, Customer user)
    {
        if (user.Id == 0 || CustomerGuildPolicy.IsExempt(user)) return IdentityResult.Success;
        var context = db.Value;
        var binding = context.ChangeTracker.Entries<SmsLoginPhone>()
            .FirstOrDefault(e => e.Entity.CustomerId == user.Id && e.State != EntityState.Deleted)?.Entity;
        var phone = binding?.Phone ?? await context.Set<SmsLoginPhone>().AsNoTracking()
            .Where(x => x.CustomerId == user.Id).Select(x => x.Phone).FirstOrDefaultAsync();
        return phone == null || user.Username == SmsPhoneNormalizer.ToProviderPhone(phone)
            ? IdentityResult.Success
            : IdentityResult.Failed(new IdentityError { Code = "username", Description = "username_must_match_verified_phone" });
    }
}
