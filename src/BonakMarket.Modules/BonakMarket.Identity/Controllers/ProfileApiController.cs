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
[ApiExplorerSettings(GroupName = "identity")]
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
    [ProducesResponseType(typeof(CustomerProfileDto), 200)]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var (customer, error) = await RequireAccountAsync();
        if (error is not null) return error;
        return Ok(await ReadProfileAsync(customer, ct));
    }

    /// <summary>Updates the same native profile as POST profile and returns the current representation.</summary>
    [HttpPut("profile"), HttpPut("~/api/sms-login/profile"), AllowAnonymous, IgnoreAntiforgeryToken]
    [ProducesResponseType(typeof(CustomerProfileDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 422)]
    public async Task<IActionResult> UpdateProfile([FromBody] ProfileUpdateRequest request, CancellationToken ct)
    {
        var (customer, error) = await RequireAccountAsync();
        if (error is not null) return error;
        var result = await _profile.CompleteProfileAsync(customer, request.FirstName, request.LastName, request.Email, request.NationalCode, request.GuildId, ct);
        if (!result.Succeeded)
        {
            var problem = new ValidationProblemDetails(result.Errors.GroupBy(x => x.Code ?? "profile")
                .ToDictionary(x => x.Key, x => x.Select(y => y.Description).ToArray()))
                { Status = 422, Title = "validation_failed" };
            return new ObjectResult(problem) { StatusCode = 422, ContentTypes = { "application/problem+json" } };
        }
        return Ok(await ReadProfileAsync(customer, ct));
    }

    [HttpGet("addresses"), HttpGet("~/api/sms-login/profile/addresses"), AllowAnonymous]
    [ProducesResponseType(typeof(List<CustomerAddressDto>), 200)]
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
    [ProducesResponseType(typeof(IdentityCodeResponse), 200)]
    public async Task<IActionResult> CompleteProfile([FromBody] ProfileUpdateRequest request, CancellationToken ct)
    {
        var (customer, error) = await RequireAccountAsync();
        if (error is not null) return error;

        var result = await _profile.CompleteProfileAsync(
            customer, request.FirstName, request.LastName, request.Email, request.NationalCode, request.GuildId, ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { code = "invalid_profile", errors = result.Errors.Select(x => x.Description) });
        }

        return Ok(new IdentityCodeResponse("profile_saved"));
    }

    private async Task<CustomerProfileDto> ReadProfileAsync(Customer customer, CancellationToken ct)
    {
        var phone = await _profile.GetVerifiedPhoneAsync(customer.Id, ct);
        var required = await _profile.IsProfileIncompleteAsync(customer.Id, ct);
        var guild = await _db.Guilds.AsNoTracking().Where(x => x.Id == customer.GuildId).Select(x => new GuildDto(x.Id, x.Code, x.Title, x.IsActive)).FirstOrDefaultAsync(ct);
        var email = customer.Email?.EndsWith("@accounts.invalid", StringComparison.OrdinalIgnoreCase) == true ? null : customer.Email;
        return new(customer.Id, customer.Username, customer.FirstName, customer.LastName, email, phone, required, customer.NationalCode, guild, required ? new[] { "guildId" } : Array.Empty<string>());
    }
}
