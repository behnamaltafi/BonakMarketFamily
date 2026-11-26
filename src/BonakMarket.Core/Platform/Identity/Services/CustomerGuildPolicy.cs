using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;

namespace BonakMarket.Core.Identity;

public static class CustomerGuildPolicy
{
    public static bool IsExempt(Customer customer) => customer.IsSystemAccount ||
        customer.CustomerRoleMappings.Any(x => x.CustomerRole != null && x.CustomerRole.Active &&
            (x.CustomerRole.SystemName == SystemCustomerRoleNames.Administrators ||
             x.CustomerRole.SystemName == SystemCustomerRoleNames.SuperAdministrators));

    public static async Task<bool> RequiresGuildAsync(SmartDbContext db, Customer customer, CancellationToken ct = default)
        => customer != null && customer.IsRegistered() && !IsExempt(customer) &&
            (!customer.GuildId.HasValue || !await db.Guilds.AsNoTracking().AnyAsync(x => x.Id == customer.GuildId.Value && x.IsActive, ct));
}
