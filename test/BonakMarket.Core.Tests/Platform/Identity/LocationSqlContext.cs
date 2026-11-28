using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using BonakMarket.Core.Common;

namespace BonakMarket.Core.Tests.Platform.Identity;

internal sealed class LocationSqlContext(DbContextOptions<LocationSqlContext> options) : DbContext(options)
{
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Conventions.Remove(typeof(RelationshipDiscoveryConvention));
        configurationBuilder.Conventions.Remove(typeof(IndexAttributeConvention));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        Map<StateProvince>(modelBuilder, "StateProvince", "Id", "CountryId", "Name", "Published", "DisplayOrder");
        Map<City>(modelBuilder, "City", "Id", "StateProvinceId", "Name", "Published", "DisplayOrder", "StateProvince");
        modelBuilder.Entity<City>().HasOne(c => c.StateProvince).WithMany().HasForeignKey(c => c.StateProvinceId);
    }

    private static void Map<TEntity>(ModelBuilder modelBuilder, string table, params string[] included)
        where TEntity : class
    {
        var entity = modelBuilder.Entity<TEntity>();
        var keep = new HashSet<string>(included);
        foreach (var property in typeof(TEntity).GetProperties())
        {
            if (!keep.Contains(property.Name))
                entity.Ignore(property.Name);
        }
        entity.HasKey("Id");
        entity.ToTable(table);
    }
}
