using FluentMigrator;
using BonakMarket.Core.Catalog.Products;

namespace BonakMarket.Core.Data.Migrations;

[MigrationVersion("2022-10-21 11:00:00", "Core: Verfied Purchase for Reviews")]
internal class VerifiedPurchase : Migration
{
    public override void Up()
    {
        var propTableName = nameof(ProductReview);
        Create.Column(nameof(ProductReview.IsVerifiedPurchase)).OnTable(propTableName).AsBoolean().Nullable();
    }

    public override void Down()
    {
    }
}