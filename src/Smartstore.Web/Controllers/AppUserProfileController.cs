using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Smartstore.Core.Identity;

namespace Smartstore.Web.Controllers;

/// <summary>
/// Exposes person-owned profile resources without crossing the boundary into
/// the commercial customer selected for order placement.
/// </summary>
[ApiController]
[Authorize]
[Route("api/app-user/profile")]
public sealed class AppUserProfileController : PublicController
{
    private readonly ICurrentAppUserContext _currentUser;
    private readonly IAppUserProfileService _profile;
    private readonly IAppUserCustomerService _memberships;

    public AppUserProfileController(
        ICurrentAppUserContext currentUser,
        IAppUserProfileService profile,
        IAppUserCustomerService memberships)
    {
        _currentUser = currentUser;
        _profile = profile;
        _memberships = memberships;
    }

    [HttpGet]
    public async Task<IActionResult> GetProfile(CancellationToken cancelToken)
    {
        var user = await _currentUser.GetCurrentUserAsync(cancelToken);
        if (user is null)
            return Unauthorized();

        var customers = await _memberships.GetAccessibleCustomerIdsAsync(user.Id, cancelToken);
        return Ok(new
        {
            user.Id,
            user.UserGuid,
            user.Username,
            user.Email,
            user.PhoneNumber,
            user.PhoneVerified,
            user.DefaultBillingAddressId,
            user.DefaultShippingAddressId,
            customerIds = customers
        });
    }

    [HttpGet("addresses")]
    public async Task<IActionResult> GetAddresses(CancellationToken cancelToken)
    {
        if (await _currentUser.GetCurrentUserAsync(cancelToken) is null)
            return Unauthorized();

        var addresses = await _profile.GetCurrentUserAddressesAsync(cancelToken);
        return Ok(addresses.Select(x => new
        {
            x.Id,
            x.FirstName,
            x.LastName,
            x.Email,
            x.Address1,
            x.Address2,
            x.City,
            x.ZipPostalCode,
            x.PhoneNumber,
            x.CountryId,
            x.StateProvinceId
        }));
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles(CancellationToken cancelToken)
    {
        if (await _currentUser.GetCurrentUserAsync(cancelToken) is null)
            return Unauthorized();

        var roles = await _profile.GetCurrentUserRolesAsync(cancelToken);
        return Ok(roles.Select(x => new { x.Id, x.Name, x.SystemName }));
    }

    [HttpGet("cart")]
    public async Task<IActionResult> GetCart(CancellationToken cancelToken)
    {
        if (await _currentUser.GetCurrentUserAsync(cancelToken) is null)
            return Unauthorized();

        var items = await _profile.GetCurrentUserCartItemsAsync(cancelToken);
        return Ok(items.Select(x => new
        {
            x.Id,
            x.ProductId,
            x.StoreId,
            x.Quantity,
            x.ShoppingCartTypeId
        }));
    }
}
