using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using BonakMarket.Core.Identity;
using BonakMarket.Identity.Models;
using BonakMarket.Identity.Controllers;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class CustomerOnboardingContractTests
{
    [TestCase(null, true)]
    [TestCase("", true)]
    [TestCase("۰۰۱۳۵۴۸۷۶۹", false)]
    [TestCase("0000000000", false)]
    [TestCase("1234567890", false)]
    [TestCase("0012345679", true)]
    [TestCase("۰۰۱۲۳۴۵۶۷۹", true)]
    [TestCase("001234567", false)]
    public void National_code_is_optional_and_checksum_validated(string input, bool expected)
        => Assert.That(NationalCodeValidator.IsValid(input), Is.EqualTo(expected));

    [Test]
    public void Profile_request_exposes_only_editable_fields()
        => Assert.That(typeof(ProfileUpdateRequest).GetProperties().Select(x => x.Name),
            Is.EquivalentTo(new[] { "FirstName", "LastName", "Email", "NationalCode", "GuildId" }));

    [Test]
    public void Address_request_does_not_allow_customer_country_or_email_override()
    {
        var names = typeof(AddressUpdateRequest).GetProperties().Select(x => x.Name).ToArray();
        Assert.That(names, Does.Not.Contain("CustomerId"));
        Assert.That(names, Does.Not.Contain("CountryId"));
        Assert.That(names, Does.Not.Contain("Email"));
        Assert.That(names, Does.Not.Contain("Id"));
    }

    [Test]
    public void Account_security_dto_does_not_pollute_login_request()
    {
        Assert.That(typeof(SmsCodeRequest).GetProperty("ReauthToken"), Is.Null);
        Assert.That(typeof(PhoneChangeRequest).GetProperty("ReauthToken"), Is.Not.Null);
    }

    [Test]
    public void Address_routes_are_separate_from_profile()
    {
        Assert.That(typeof(CustomerAddressApiController).GetCustomAttribute<RouteAttribute>()?.Template,
            Is.EqualTo("api/identity/v1/me/addresses"));
        Assert.That(typeof(GuildApiController).GetCustomAttribute<RouteAttribute>()?.Template,
            Is.EqualTo("api/identity/v1/guilds"));
    }
}
