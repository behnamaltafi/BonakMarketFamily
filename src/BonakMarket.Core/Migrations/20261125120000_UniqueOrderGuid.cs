using FluentMigrator;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Data.Migrations;

namespace BonakMarket.Core.Migrations;

[MigrationVersion("2026-11-25 12:00:00", "Core: Unique order GUID")]
internal sealed class UniqueOrderGuid : Migration
{
    private const string TableName = nameof(Order);
    private const string ColumnName = nameof(Order.OrderGuid);
    private const string OldIndex = "IX_Order_OrderGuid";
    private const string UniqueIndex = "UX_Order_OrderGuid";

    public override void Up()
    {
        var order = Schema.Table(TableName);
        // Creating the unique index validates ALL existing rows (including soft-deleted orders).
        // If duplicate GUIDs exist the database rejects the migration. Never rewrite historical IDs.
        // Create before dropping the old index so a failure leaves the previous index intact.
        if (!order.Index(UniqueIndex).Exists())
            Create.Index(UniqueIndex).OnTable(TableName).OnColumn(ColumnName).Ascending().WithOptions().Unique();

        if (order.Index(OldIndex).Exists())
            Delete.Index(OldIndex).OnTable(TableName);
    }

    public override void Down()
    {
        var order = Schema.Table(TableName);
        if (!order.Index(OldIndex).Exists())
            Create.Index(OldIndex).OnTable(TableName).OnColumn(ColumnName).Ascending();
        if (order.Index(UniqueIndex).Exists())
            Delete.Index(UniqueIndex).OnTable(TableName);
    }
}
