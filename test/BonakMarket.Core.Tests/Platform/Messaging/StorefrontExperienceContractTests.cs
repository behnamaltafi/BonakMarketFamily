using System.Linq;
using NUnit.Framework;
using BonakMarket.StorefrontApi.Controllers;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class StorefrontExperienceContractTests
{
    [Test]
    public void Public_pages_and_bootstrap_do_not_require_customer_login()
    {
        foreach (var method in new[] { "Bootstrap", "Pages", "Page" })
            Assert.That(typeof(StorefrontExperienceController).GetMethod(method).IsDefined(typeof(PublicCatalogAttribute), true), Is.True);
    }

    [Test]
    public void Customer_summary_and_reviews_are_private()
    {
        foreach (var method in new[] { "Summary", "MyReviews" })
            Assert.That(typeof(CustomerServicesController).GetMethod(method).IsDefined(typeof(PublicCatalogAttribute), true), Is.False);
    }

    [Test]
    public void Page_contract_has_no_password_or_acl_data()
        => Assert.That(typeof(TopicDetailDto).GetProperties().Select(x => x.Name),
            Is.EquivalentTo(new[] { "Id", "SystemName", "Title", "Intro", "BodyHtml", "MetaTitle", "MetaDescription" }));
}
