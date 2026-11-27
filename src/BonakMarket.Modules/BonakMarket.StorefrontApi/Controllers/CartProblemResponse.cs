using Microsoft.AspNetCore.Mvc;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Controllers;

internal static class CartProblemResponse
{
    internal static async Task<ObjectResult> FailureAsync(CartChange result, CartFacade cart)
    {
        var problem = new ProblemDetails { Status = result.Status, Title = result.Code };
        if (result.Warnings?.Count > 0 && await cart.CanDisplayPriceAsync()) problem.Extensions["warnings"] = result.Warnings;
        return new ObjectResult(problem) { StatusCode = result.Status, ContentTypes = { "application/problem+json" } };
    }
}
