using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Common.DTO;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.StorefrontApi.Models;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1/profile")]
public sealed class ProfileController(SmartDbContext db) : StorefrontController
{
    private IStorefrontSmsProfileService Sms => HttpContext.RequestServices.GetService<IStorefrontSmsProfileService>();

    private async Task<ProfileDto> Model(CancellationToken ct)
    {
        var phone = Sms == null ? null : await Sms.GetVerifiedPhoneAsync(Customer.Id, ct);
        var required = phone != null && await Sms.IsProfileIncompleteAsync(Customer.Id, ct);
        var email = Customer.Email?.EndsWith("@accounts.invalid", StringComparison.OrdinalIgnoreCase) == true ? null : Customer.Email;
        return new ProfileDto(Customer.Id, Customer.Username, Customer.FirstName, Customer.LastName, email, phone, required);
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<ProfileDto>), 200)]
    public async Task<IActionResult> Get(CancellationToken ct) => Success(await Model(ct));

    // Reuse the verified-phone onboarding validation; do not accept a CustomerId or change phone here.
    [HttpPut]
    [ProducesResponseType(typeof(ApiResult<ProfileDto>), 200)]
    public async Task<IActionResult> Update([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        if (Sms == null) return Error(503, "sms_profile_unavailable");
        var result = await Sms.CompleteProfileAsync(Customer, request.Username, request.FirstName, request.LastName, request.Email, ct);
        if (!result.Succeeded)
            return StatusCode(422, new ApiResult("invalid_profile", false, result.Errors.Select(x => x.Description).ToArray(), 422));
        return Success(await Model(ct));
    }

    [HttpGet("addresses")]
    [ProducesResponseType(typeof(ApiResult<List<AddressDto>>), 200)]
    public async Task<IActionResult> Addresses(CancellationToken ct)
    {
        var addresses = await db.Customers.AsNoTracking().Where(x => x.Id == Customer.Id)
            .SelectMany(x => x.Addresses).OrderBy(x => x.Id)
            .Select(x => new AddressDto(x.Id, x.FirstName, x.LastName, x.CountryId, x.StateProvinceId,
                x.City, x.Address1, x.Address2, x.ZipPostalCode, x.PhoneNumber)).ToListAsync(ct);
        return Success(addresses);
    }
}
