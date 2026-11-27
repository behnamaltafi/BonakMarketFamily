using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Common.Services;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.StorefrontApi.Domain;
using BonakMarket.StorefrontApi.Models;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Services;

public sealed record GuestCartMergeResult(int Status, string Code = null, IReadOnlyList<GuestCartConflictDto> Conflicts = null, string Warning = null);
