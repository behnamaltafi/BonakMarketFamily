#nullable enable
using System.Text.Json;
using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BonakMarket.Core.Checkout.Attributes;
using BonakMarket.Core.Checkout.Cart.Events;
using BonakMarket.Core.Stores;
using BonakMarket.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Checkout.Payment;
using BonakMarket.Core.Checkout.Shipping;
using BonakMarket.Core.Common;
using BonakMarket.Core.Common.Services;
using BonakMarket.Core.Data;
using BonakMarket.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Security;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.StorefrontApi.Services;
using BonakMarket.Threading;

namespace BonakMarket.StorefrontApi.Controllers;

internal sealed record CheckoutLoadResult(CheckoutDraft? Draft = null, CartStateDto? Cart = null,
    ShoppingCart? Native = null, ObjectResult? Error = null);
