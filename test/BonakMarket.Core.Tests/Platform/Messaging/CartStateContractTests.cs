using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using BonakMarket.StorefrontApi.Controllers;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class CartStateContractTests
{
    [Test]
    public void Cart_and_wishlist_return_state_not_arrays()
    {
        Assert.That(typeof(CartController).GetMethod(nameof(CartController.Get)).ReturnType,
            Is.EqualTo(typeof(Task<ActionResult<CartStateDto>>)));
        Assert.That(typeof(WishlistController).GetMethod(nameof(WishlistController.Get)).ReturnType,
            Is.EqualTo(typeof(Task<ActionResult<WishlistStateDto>>)));
        Assert.That(typeof(CartController).GetMethod(nameof(CartController.ToWishlist)).ReturnType,
            Is.EqualTo(typeof(Task<ActionResult<CartTransferDto>>)));
        Assert.That(typeof(WishlistController).GetMethod(nameof(WishlistController.ToCart)).ReturnType,
            Is.EqualTo(typeof(Task<ActionResult<CartTransferDto>>)));
    }

    [TestCase(null, null, false)]
    [TestCase(null, false, true)]
    [TestCase(1, null, true)]
    [TestCase(0, null, false)]
    [TestCase(1001, true, false)]
    public void Update_requires_quantity_or_active(int? quantity, bool? active, bool expected)
    {
        var dto = new UpdateCartItem { Quantity = quantity, Active = active };
        Assert.That(Validator.TryValidateObject(dto, new ValidationContext(dto), new List<ValidationResult>(), true),
            Is.EqualTo(expected));
    }

    [Test]
    public void Revision_is_deterministic_and_changes_with_quantity()
    {
        var first = CartSnapshotRevision.Create(new { Id = 1, Quantity = 2 });
        Assert.That(first.Length, Is.EqualTo(64));
        Assert.That(CartSnapshotRevision.Create(new { Id = 1, Quantity = 2 }), Is.EqualTo(first));
        Assert.That(CartSnapshotRevision.Create(new { Id = 1, Quantity = 3 }), Is.Not.EqualTo(first));
    }

    [Test]
    public void Gift_card_response_does_not_expose_full_coupon_code()
        => Assert.That(typeof(CartGiftCardDto).GetProperties().Select(x => x.Name),
            Is.EquivalentTo(new[] { "Id", "MaskedCode", "UsableAmount", "AppliedAmount" }));
}
