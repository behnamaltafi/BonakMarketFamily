using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Autofac;
using BonakMarket.Core;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Catalog.Categories;
using BonakMarket.Core.Catalog.Brands;
using BonakMarket.Core.Catalog;
using BonakMarket.Core.Catalog.Attributes;
using BonakMarket.Core.Catalog.Search;
using BonakMarket.Core.Common;
using BonakMarket.Core.Catalog.Pricing;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Configuration;
using BonakMarket.StorefrontApi.Configuration;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Content.Media;
using BonakMarket.Core.Data;
using BonakMarket.Core.Security;
using BonakMarket.Core.Stores;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Services;

internal sealed record ProductPurchaseData(HashSet<int> Sellable,
    Dictionary<int, (int MaxStock, bool Backorder)> CombinationCapacity,
    Dictionary<int, (bool HasVariants, bool UnsupportedRequired)> VariantInfo,
    Dictionary<int, QuantityUnitDto> Units, QuantityUnitDto DefaultUnit, bool CanCart);
