using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BonakMarket.Core.Common;

internal class CityMap : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.HasOne(x => x.StateProvince).WithMany().HasForeignKey(x => x.StateProvinceId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.StateProvinceId, x.Name }).IsUnique();
    }
}
