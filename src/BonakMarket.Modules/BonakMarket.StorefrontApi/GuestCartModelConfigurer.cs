using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;
using BonakMarket.Data;
using BonakMarket.Data.Providers;
using BonakMarket.StorefrontApi.Domain;
using BonakMarket.Engine;
using BonakMarket.Engine.Builders;
using BonakMarket.StorefrontApi.Security;
using BonakMarket.StorefrontApi.Services;
using BonakMarket.StorefrontApi.Queries;
using BonakMarket.StorefrontApi.Swagger;

namespace BonakMarket.StorefrontApi;

internal sealed class GuestCartModelConfigurer : IDbContextConfigurationSource<SmartDbContext>
{
    public void Configure(IServiceProvider services, DbContextOptionsBuilder builder) =>
        builder.UseDbFactory(factory => factory.AddModelAssembly(typeof(GuestCartToken).Assembly));
}
