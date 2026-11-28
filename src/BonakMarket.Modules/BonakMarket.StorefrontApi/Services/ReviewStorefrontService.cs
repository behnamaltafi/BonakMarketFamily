using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Catalog;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Localization;
using BonakMarket.Core.Logging;
using BonakMarket.Core.Messaging;
using BonakMarket.Core.Security;
using BonakMarket.Core.Web;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.Utilities;
using BonakMarket.Utilities.Html;

namespace BonakMarket.StorefrontApi.Services;

public sealed class ReviewStorefrontService(SmartDbContext db, StorefrontRequestContext request, CatalogSettings settings,
    PrivacySettings privacy, IProductService products, ICustomerService customers, IWebHelper web,
    IActivityLogger activity, IMessageFactory messages, LocalizationSettings localization)
{
    private IQueryable<Product> Visible(int productId) => StorefrontScope.Products(db, request.Customer, request.StoreId, forDetail: true)
        .Where(x => x.Id == productId && !x.IsSystemProduct && x.AllowCustomerReviews);

    public async Task<StorefrontResult<PageDto<CustomerReviewDto>>> ListAsync(int productId, PageRequest page, CancellationToken ct)
    {
        if (!await Visible(productId).AnyAsync(ct)) return new StorefrontFailure(404, "product_not_found");
        return new(await db.ProductReviews.AsNoTracking().Where(x => x.ProductId == productId && x.IsApproved)
            .OrderByDescending(x => x.CreatedOnUtc).ThenByDescending(x => x.Id)
            .Select(x => new CustomerReviewDto(x.Id, x.Title, x.ReviewText, x.Rating, x.HelpfulYesTotal,
                x.HelpfulNoTotal, x.CreatedOnUtc, x.CustomerId == request.Customer.Id)).PageAsync(page, ct));
    }

    public async Task<StorefrontResult<ReviewResultDto>> CreateAsync(int productId, ReviewRequest input, CancellationToken ct)
    {
        if (privacy.DisplayGdprConsentOnForms && !input.GdprConsent) return new StorefrontFailure(422, "gdpr_consent_required");
        var product = await Visible(productId).FirstOrDefaultAsync(ct);
        if (product == null) return new StorefrontFailure(404, "product_not_found");
        var title = input.Title?.RemoveHtml()?.Trim();
        var review = HtmlUtility.SanitizeHtml(input.ReviewText, HtmlSanitizerOptions.UserCommentSuitable);
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(review)) return new StorefrontFailure(422, "invalid_review");
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (input.GdprConsent) request.Customer.GenericAttributes.HasConsentedToGdpr = true;
        var entity = new ProductReview
        {
            ProductId = product.Id, CustomerId = request.Customer.Id, IpAddress = web.ClientInfo.IpAddress.ToString(),
            Title = title, ReviewText = review, Rating = input.Rating, IsApproved = !settings.ProductReviewsMustBeApproved,
            Product = product
        };
        product.ProductReviews.Add(entity);
        await db.SaveChangesAsync(ct);
        await products.ApplyProductReviewTotalsAsync([product], ct);
        if (entity.IsApproved) customers.ApplyRewardPointsForProductReview(request.Customer, product, true);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        activity.LogActivity(KnownActivityLogTypes.PublicStoreAddProductReview, "Product review added", product.Name);
        if (settings.NotifyStoreOwnerAboutNewProductReviews)
            await messages.SendProductReviewNotificationMessageAsync(entity, localization.DefaultAdminLanguageId);
        return new(new ReviewResultDto(entity.Id, entity.IsApproved));
    }

    public async Task<StorefrontResult<ReviewVoteDto>> VoteAsync(int productId, int reviewId, ReviewVoteRequest input, CancellationToken ct)
    {
        if (!await Visible(productId).AnyAsync(ct)) return new StorefrontFailure(404, "product_not_found");
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var review = await db.ProductReviews.FirstOrDefaultAsync(x => x.Id == reviewId && x.ProductId == productId && x.IsApproved, ct);
        if (review == null) return new StorefrontFailure(404, "review_not_found");
        if (review.CustomerId == request.Customer.Id) return new StorefrontFailure(403, "own_review_vote_not_allowed");
        var entries = db.CustomerContent.OfType<ProductReviewHelpfulness>().Where(x => x.ProductReviewId == reviewId);
        var old = await entries.Where(x => x.CustomerId == request.Customer.Id).ToListAsync(ct);
        db.CustomerContent.RemoveRange(old);
        db.CustomerContent.Add(new ProductReviewHelpfulness { ProductReviewId = reviewId, CustomerId = request.Customer.Id,
            WasHelpful = input.WasHelpful, IsApproved = true, IpAddress = web.ClientInfo.IpAddress.ToString() });
        await db.SaveChangesAsync(ct);
        review.HelpfulYesTotal = await entries.CountAsync(x => x.WasHelpful, ct);
        review.HelpfulNoTotal = await entries.CountAsync(x => !x.WasHelpful, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(new ReviewVoteDto(review.HelpfulYesTotal, review.HelpfulNoTotal));
    }
}
