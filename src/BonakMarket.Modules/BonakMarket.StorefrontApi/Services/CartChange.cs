using Microsoft.EntityFrameworkCore;
using BonakMarket.Core;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Catalog.Attributes;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Data;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Services;

public sealed record CartChange(int Status, string Code = null, List<string> Warnings = null, List<CartItemDto> Items = null);
