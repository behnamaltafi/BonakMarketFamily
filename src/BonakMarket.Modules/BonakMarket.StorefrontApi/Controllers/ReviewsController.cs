using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Security;
using BonakMarket.Core.Web;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Controllers;

[Route("api/storefront/v1/products/{productId:int}/reviews")]
[StorefrontPermission(StorefrontPermissions.Reviews)]
[StorefrontPermission(StorefrontPermissions.Catalog)]
public sealed class ReviewsController(ReviewStorefrontService reviews) : StorefrontController
{
    [HttpGet]
    public async Task<ActionResult<PageDto<CustomerReviewDto>>> List(int productId, [FromQuery] PageRequest page, CancellationToken ct) =>
        Respond(await reviews.ListAsync(productId, page, ct));

    // Native CAPTCHA providers read form fields, not JSON. Do not allow JSON to bypass the native filter.
    [HttpPost]
    [Consumes("application/x-www-form-urlencoded", "multipart/form-data")]
    [ValidateCaptcha(CaptchaSettings.Targets.ProductReview), RequireCaptchaResult]
    [ProducesResponseType(typeof(ReviewResultDto), 201)]
    public async Task<ActionResult<ReviewResultDto>> Create(int productId, [FromForm] ReviewRequest input, CancellationToken ct)
    {
        if (!Request.HasFormContentType || !ModelState.IsValid) return ValidationProblem(ModelState);
        return Respond(await reviews.CreateAsync(productId, input, ct), 201);
    }

    [HttpPut("{reviewId:int}/helpfulness")]
    public async Task<ActionResult<ReviewVoteDto>> Vote(int productId, int reviewId, [FromBody] ReviewVoteRequest input, CancellationToken ct) =>
        Respond(await reviews.VoteAsync(productId, reviewId, input, ct));
}
