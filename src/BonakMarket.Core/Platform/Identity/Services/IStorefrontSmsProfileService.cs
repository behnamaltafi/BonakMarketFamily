using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace BonakMarket.Core.Identity;

/// <summary>Optional native SMS profile completion, shared by storefront clients.</summary>
public interface IStorefrontSmsProfileService
{
    Task<bool> IsProfileIncompleteAsync(int customerId, CancellationToken ct = default);
    Task<string> GetVerifiedPhoneAsync(int customerId, CancellationToken ct = default);
    Task<IdentityResult> CompleteProfileAsync(Customer customer, string firstName, string lastName, string email, string nationalCode, int? guildId, CancellationToken ct = default);
}
