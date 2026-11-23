using Microsoft.EntityFrameworkCore;

namespace BonakMarket.Data;

public interface IDbContextConfigurationSource<TContext>
    where TContext : DbContext
{
    void Configure(IServiceProvider services, DbContextOptionsBuilder builder);
}