using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using BonakMarket.Identity.Controllers;
using BonakMarket.Identity.Models;
using BonakMarket.StorefrontApi.Models;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class AddressGeographyContractTests
{
    [Test]
    public void Omitted_coordinates_are_distinguished_from_explicit_null()
    {
        var omitted = JsonSerializer.Deserialize<AddressUpdateRequest>("{}");
        var cleared = JsonSerializer.Deserialize<AddressUpdateRequest>("{\"Latitude\":null,\"Longitude\":null}");
        Assert.That(omitted.LatitudeSpecified, Is.False);
        Assert.That(omitted.LongitudeSpecified, Is.False);
        Assert.That(cleared.LatitudeSpecified, Is.True);
        Assert.That(cleared.LongitudeSpecified, Is.True);
    }

    [TestCase(typeof(CustomerAddressDto))]
    [TestCase(typeof(OrderAddressDto))]
    public void Address_responses_expose_geography(System.Type contract)
    {
        Assert.That(contract.GetProperty("CityId")?.PropertyType, Is.EqualTo(typeof(int?)));
        Assert.That(contract.GetProperty("Latitude")?.PropertyType, Is.EqualTo(typeof(decimal?)));
        Assert.That(contract.GetProperty("Longitude")?.PropertyType, Is.EqualTo(typeof(decimal?)));
    }

    [TestCase(nameof(LocationController.Provinces), "provinces")]
    [TestCase(nameof(LocationController.Cities), "provinces/{id:int}/cities")]
    public void Location_routes_are_public(string method, string route)
    {
        var action = typeof(LocationController).GetMethod(method);
        Assert.That(action.GetCustomAttributes(typeof(AllowAnonymousAttribute), true), Is.Not.Empty);
        Assert.That(((HttpGetAttribute)action.GetCustomAttributes(typeof(HttpGetAttribute), true)[0]).Template, Is.EqualTo(route));
    }
}
