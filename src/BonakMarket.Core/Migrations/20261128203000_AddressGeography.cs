using FluentMigrator;
using BonakMarket.Core.Common.Services;
using BonakMarket.Core.Data;
using BonakMarket.Core.Data.Migrations;
using BonakMarket.Data.Migrations;

namespace BonakMarket.Core.Migrations;

[MigrationVersion("2026-11-28 20:30:00", "Core: Address geography")]
internal sealed class AddressGeographyMigration : Migration, IDataSeeder<SmartDbContext>
{
    public DataSeederStage Stage => DataSeederStage.Early;
    public bool AbortOnFailure => true;

    public override void Up()
    {
        if (!Schema.Table("City").Exists())
        {
            Create.Table("City").WithIdColumn()
                .WithColumn("StateProvinceId").AsInt32().NotNullable()
                .WithColumn("Name").AsString(100).NotNullable()
                .WithColumn("Published").AsBoolean().NotNullable()
                .WithColumn("DisplayOrder").AsInt32().NotNullable().WithDefaultValue(0);
            Create.ForeignKey("FK_City_StateProvince").FromTable("City").ForeignColumn("StateProvinceId")
                .ToTable("StateProvince").PrimaryColumn("Id");
            Create.Index("IX_City_StateProvinceId_Name").OnTable("City").OnColumn("StateProvinceId")
                .Ascending().OnColumn("Name").Ascending().WithOptions().Unique();
        }

        if (!Schema.Table("Address").Column("CityId").Exists())
        {
            Create.Column("CityId").OnTable("Address").AsInt32().Nullable();
            Create.ForeignKey("FK_Address_City").FromTable("Address").ForeignColumn("CityId")
                .ToTable("City").PrimaryColumn("Id");
            Create.Index("IX_Address_CityId").OnTable("Address").OnColumn("CityId").Ascending();
        }
        if (!Schema.Table("Address").Column("Latitude").Exists())
            Create.Column("Latitude").OnTable("Address").AsDecimal(9, 6).Nullable();
        if (!Schema.Table("Address").Column("Longitude").Exists())
            Create.Column("Longitude").OnTable("Address").AsDecimal(9, 6).Nullable();
    }

    public Task SeedAsync(SmartDbContext context, CancellationToken cancelToken = default)
        => IranGeographySeed.SeedAsync(context, cancelToken);

    public override void Down()
    {
        // Preserve address and order snapshot data on rollback.
    }
}
