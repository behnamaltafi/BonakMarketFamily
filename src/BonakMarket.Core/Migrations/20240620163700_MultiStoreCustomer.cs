using FluentMigrator;
using BonakMarket.Core.Identity;

namespace BonakMarket.Core.Data.Migrations;

[MigrationVersion("2024-06-20 16:37:00", "Core: MultiStoreCustomer")]
internal class MultiStoreCustomer : Migration
{
    public override void Up()
    {
        var tableName = nameof(Customer);
        var limitedToStores = nameof(Customer.LimitedToStores);

        if (!Schema.Table(tableName).Column(limitedToStores).Exists())
        {
            Create.Column(limitedToStores).OnTable(tableName).AsBoolean().NotNullable().WithDefaultValue(false);
        }
    }

    public override void Down()
    {
    }
}