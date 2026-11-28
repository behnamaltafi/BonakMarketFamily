using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using NUnit.Framework;
using BonakMarket.Core.Common;
using BonakMarket.Identity.Queries;

namespace BonakMarket.Core.Tests.Platform.Identity;

[TestFixture]
public sealed class CustomerAddressQueriesTests
{
    [Test]
    public void Linked_city_uses_current_name_even_when_unpublished_without_mutating_snapshot()
    {
        var address = new Address { Id = 1, CityId = 7, City = "Old name", Latitude = 35.7m, Longitude = 51.3m };
        var snapshot = (Address)address.Clone();
        var city = new City { Id = 7, Name = "Renamed city", Published = false };

        var result = CustomerAddressQueries.Project(new[] { address }.AsQueryable(),
            new[] { city }.AsQueryable(), 1, null).Single();

        Assert.That(result.City, Is.EqualTo("Renamed city"));
        Assert.That(result.CityId, Is.EqualTo(7));
        Assert.That(result.IsDefaultBilling, Is.True);
        Assert.That(result.IsDefaultShipping, Is.False);
        Assert.That(result.Latitude, Is.EqualTo(35.7m));
        Assert.That(result.Longitude, Is.EqualTo(51.3m));
        Assert.That(address.City, Is.EqualTo("Old name"));
        Assert.That(snapshot.City, Is.EqualTo("Old name"));
    }

    [TestCase(null)]
    [TestCase(99)]
    public void Missing_city_link_preserves_legacy_text(int? cityId)
    {
        var address = new Address { Id = 1, CityId = cityId, City = "Legacy city" };
        var city = new City { Id = 7, Name = "Other city", Published = true };

        var result = CustomerAddressQueries.Project(new[] { address }.AsQueryable(),
            new[] { city }.AsQueryable(), null, 1).Single();

        Assert.That(result.City, Is.EqualTo("Legacy city"));
        Assert.That(result.CityId, Is.EqualTo(cityId));
        Assert.That(result.IsDefaultShipping, Is.True);
    }

    [Test]
    public void City_projection_translates_with_server_side_pagination()
    {
        using var db = new AddressSqlContext(new DbContextOptionsBuilder<AddressSqlContext>()
            .UseSqlServer("Server=localhost;Database=BonakMarketTranslationOnly;Integrated Security=True;TrustServerCertificate=True").Options);
        var addresses = db.Set<Address>().AsNoTracking().Where(a => a.Id > 0).OrderBy(a => a.Id).Skip(1).Take(5);
        var sql = CustomerAddressQueries.Project(addresses, db.Set<City>(), 1, null).ToQueryString();

        Assert.That(sql, Does.Contain("COALESCE").IgnoreCase);
        Assert.That(sql, Does.Contain("[City]").IgnoreCase);
        Assert.That(sql, Does.Contain("[CityId]").IgnoreCase);
        Assert.That(sql, Does.Contain("OFFSET").IgnoreCase);
    }

    private sealed class AddressSqlContext(DbContextOptions<AddressSqlContext> options) : DbContext(options)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Conventions.Remove(typeof(RelationshipDiscoveryConvention));
            configurationBuilder.Conventions.Remove(typeof(IndexAttributeConvention));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            Map<Address>(modelBuilder, "Address", "Id", "FirstName", "LastName", "CountryId", "StateProvinceId",
                "City", "Address1", "Address2", "ZipPostalCode", "PhoneNumber", "Country", "StateProvince",
                "Salutation", "Title", "Company", "FaxNumber", "CityId", "Latitude", "Longitude");
            Map<City>(modelBuilder, "City", "Id", "Name");
            Map<Country>(modelBuilder, "Country", "Id", "Name");
            Map<StateProvince>(modelBuilder, "StateProvince", "Id", "Name");
            modelBuilder.Entity<Address>().HasOne(a => a.Country).WithMany().HasForeignKey(a => a.CountryId);
            modelBuilder.Entity<Address>().HasOne(a => a.StateProvince).WithMany().HasForeignKey(a => a.StateProvinceId);
        }

        private static void Map<TEntity>(ModelBuilder modelBuilder, string table, params string[] included)
            where TEntity : class
        {
            var entity = modelBuilder.Entity<TEntity>();
            var keep = new HashSet<string>(included, StringComparer.Ordinal);
            foreach (var property in typeof(TEntity).GetProperties())
            {
                if (!keep.Contains(property.Name))
                    entity.Ignore(property.Name);
            }
            entity.HasKey("Id");
            entity.ToTable(table);
        }
    }
}
