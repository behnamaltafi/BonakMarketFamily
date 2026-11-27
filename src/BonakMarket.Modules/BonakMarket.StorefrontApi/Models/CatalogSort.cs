#nullable restore
using System.ComponentModel.DataAnnotations;
using BonakMarket.StorefrontApi.Services;

namespace BonakMarket.StorefrontApi.Models;

public enum CatalogSort { Initial, NameAsc, NameDesc, PriceAsc, PriceDesc, Newest, BestSellers }
