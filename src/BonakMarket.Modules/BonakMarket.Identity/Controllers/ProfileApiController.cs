using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Identity.Models;
using BonakMarket.Identity.Services;

namespace BonakMarket.Identity.Controllers;

/// <summary>Current customer profile and saved addresses; never accepts a customer ID.</summary>
[ApiController]
[Route("api/identity/v1/me")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ProfileApiController : CustomerAccountApiController
{
    private readonly CustomerProfileService _profile;
    private readonly SmartDbContext _db;

    public ProfileApiController(CustomerProfileService profile, SignInManager<Customer> signInManager,
        UserManager<Customer> userManager, SmartDbContext db, IAntiforgery antiforgery)
        : base(signInManager, userManager, antiforgery)
    {
        _profile = profile;
        _db = db;
    }

    /// <summary>Returns the current customer's profile without accepting a customer identifier.</summary>
    [HttpGet("profile"), HttpGet("~/api/sms-login/profile"), AllowAnonymous]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var (customer, error) = await RequireAccountAsync();
        if (error is not null) return error;
        return Ok(await ReadProfileAsync(customer, ct));
    }

    /// <summary>Updates the same native profile as POST profile and returns the current representation.</summary>
    [HttpPut("profile"), HttpPut("~/api/sms-login/profile"), AllowAnonymous, IgnoreAntiforgeryToken]
    public async Task<IActionResult> UpdateProfile([FromBody] ProfileUpdateRequest request, CancellationToken ct)
    {
        var (customer, error) = await RequireAccountAsync();
        if (error is not null) return error;
        if (!await _profile.IsPhoneProfileEnabledAsync()) return NotFound();
        var result = await _profile.CompleteProfileAsync(customer, request.Username, request.FirstName, request.LastName, request.Email, ct);
        if (!result.Succeeded)
        {
            var problem = new ProblemDetails { Status = 422, Title = "invalid_profile" };
            problem.Extensions["errors"] = result.Errors.Select(x => x.Description).ToArray();
            return new ObjectResult(problem) { StatusCode = 422, ContentTypes = { "application/problem+json" } };
        }
        return Ok(await ReadProfileAsync(customer, ct));
    }

    [HttpGet("addresses"), HttpGet("~/api/sms-login/profile/addresses"), AllowAnonymous]
    public async Task<IActionResult> Addresses(CancellationToken ct)
    {
        var (customer, error) = await RequireAccountAsync();
        if (error is not null) return error;
        var addresses = await _db.Customers.AsNoTracking().Where(x => x.Id == customer.Id)
            .SelectMany(x => x.Addresses).OrderBy(x => x.Id)
            .Select(x => new CustomerAddressDto(x.Id, x.FirstName, x.LastName, x.CountryId, x.StateProvinceId,
                x.City, x.Address1, x.Address2, x.ZipPostalCode, x.PhoneNumber)).ToListAsync(ct);
        return Ok(addresses);
    }

    /// <summary>Updates the phone-linked Customer's optional profile.</summary>
    [HttpPost("~/api/sms-login/profile"), AllowAnonymous, IgnoreAntiforgeryToken]
    public async Task<IActionResult> CompleteProfile([FromBody] ProfileUpdateRequest request, CancellationToken ct)
    {
        if (!await _profile.IsPhoneProfileEnabledAsync())
        {
            return NotFound();
        }

        var (customer, error) = await RequireAccountAsync();
        if (error is not null) return error;

        var result = await _profile.CompleteProfileAsync(
            customer, request.Username, request.FirstName, request.LastName, request.Email, ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { code = "invalid_profile", errors = result.Errors.Select(x => x.Description) });
        }

        return Ok(new { code = "profile_saved" });
    }

    private async Task<CustomerProfileDto> ReadProfileAsync(Customer customer, CancellationToken ct)
    {
        var phone = await _profile.GetVerifiedPhoneAsync(customer.Id, ct);
        var required = phone != null && await _profile.IsProfileIncompleteAsync(customer.Id, ct);
        var email = customer.Email?.EndsWith("@accounts.invalid", StringComparison.OrdinalIgnoreCase) == true ? null : customer.Email;
        return new(customer.Id, customer.Username, customer.FirstName, customer.LastName, email, phone, required);
    }
}
