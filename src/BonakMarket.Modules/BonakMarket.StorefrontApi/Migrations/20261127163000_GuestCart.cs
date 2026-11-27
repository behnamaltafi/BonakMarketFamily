using FluentMigrator;
using BonakMarket.Core.Data.Migrations;
using BonakMarket.Data.Migrations;

namespace BonakMarket.StorefrontApi.Migrations;

[MigrationVersion("2026-11-27 16:30:00", "Storefront: guest cart tokens")]
internal sealed class GuestCartMigration : Migration
{
    public override void Up()
    {
        if (Schema.Table("StorefrontGuestCartToken").Exists()) return;
        Create.Table("StorefrontGuestCartToken").WithIdColumn()
            .WithColumn("TokenHash").AsString(64).NotNullable()
            .WithColumn("CustomerId").AsInt32().NotNullable()
            .WithColumn("StoreId").AsInt32().NotNullable()
            .WithColumn("CreatedOnUtc").AsDateTime2().NotNullable()
            .WithColumn("ExpiresOnUtc").AsDateTime2().NotNullable()
            .WithColumn("MergedToCustomerId").AsInt32().Nullable()
            .WithColumn("MergedOnUtc").AsDateTime2().Nullable()
            .WithColumn("RevokedOnUtc").AsDateTime2().Nullable();
        Create.Index("IX_StorefrontGuestCartToken_Hash").OnTable("StorefrontGuestCartToken")
            .OnColumn("TokenHash").Ascending().WithOptions().Unique();
        Create.Index("IX_StorefrontGuestCartToken_Customer").OnTable("StorefrontGuestCartToken")
            .OnColumn("CustomerId").Ascending().WithOptions().Unique();
        Create.Index("IX_StorefrontGuestCartToken_Expiry").OnTable("StorefrontGuestCartToken")
            .OnColumn("ExpiresOnUtc").Ascending();
    }
    public override void Down() { /* Preserve guest cart and merge receipts. */ }
}
