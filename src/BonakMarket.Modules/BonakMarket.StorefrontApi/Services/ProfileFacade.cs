using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using BonakMarket.Core.Identity;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Services;

public sealed class ProfileFacade(StorefrontRequestContext request, IServiceProvider services)
{
    private IStorefrontSmsProfileService Sms => services.GetService<IStorefrontSmsProfileService>();
    public bool CanUpdate => Sms != null;

    public async Task<ProfileDto> GetAsync(CancellationToken ct)
    {
        var customer = request.Customer;
        var sms = Sms;
        var phone = sms == null ? null : await sms.GetVerifiedPhoneAsync(customer.Id, ct);
        var required = phone != null && await sms.IsProfileIncompleteAsync(customer.Id, ct);
        var email = customer.Email?.EndsWith("@accounts.invalid", StringComparison.OrdinalIgnoreCase) == true ? null : customer.Email;
        return new ProfileDto(customer.Id, customer.Username, customer.FirstName, customer.LastName, email, phone, required);
    }

    public Task<IdentityResult> UpdateAsync(UpdateProfileRequest input, CancellationToken ct) =>
        Sms.CompleteProfileAsync(request.Customer, input.Username, input.FirstName, input.LastName, input.Email, ct);
}
