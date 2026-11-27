using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BonakMarket.Core;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Security;
using BonakMarket.Core.Stores;
using BonakMarket.StorefrontApi.Domain;

namespace BonakMarket.StorefrontApi.Security;

public sealed class StorefrontRequestContext
{
    public Customer Customer { get; internal set; }
    public int StoreId { get; internal set; }
    public bool IsGuestCart { get; internal set; }
}
