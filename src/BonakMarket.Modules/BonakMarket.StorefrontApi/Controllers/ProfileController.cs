using Microsoft.AspNetCore.Mvc;
using BonakMarket.StorefrontApi.Models;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1/profile")]
public sealed class ProfileController : StorefrontController
{
    [HttpGet]
    public IActionResult Get() => Success(new ProfileDto(Customer.Id, Customer.Username,
        Customer.FirstName, Customer.LastName, Customer.Email));
}
