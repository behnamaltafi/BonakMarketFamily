using FluentMigrator;
using BonakMarket.Core.Data.Migrations;
using BonakMarket.Data.Migrations;

namespace BonakMarket.MultiSms.Migrations;

[MigrationVersion("2026-11-24 17:00:00", "MultiSms: MobileApiSession")]
internal sealed class MobileApiSessionMigration : Migration
{
    public override void Up()
    {
        if (!Schema.Table("MultiSmsMobileApiSession").Exists())
        {
            Create.Table("MultiSmsMobileApiSession")
                .WithIdColumn()
                .WithColumn("SessionId").AsGuid().NotNullable()
                .WithColumn("CustomerId").AsInt32().NotNullable()
                .WithColumn("AccessId").AsGuid().NotNullable()
                .WithColumn("RefreshHash").AsString(64).NotNullable()
                .WithColumn("PreviousRefreshHash").AsString(64).Nullable()
                .WithColumn("VerifiedPhone").AsString(16).NotNullable()
                .WithColumn("SecurityStamp").AsString(256).Nullable()
                .WithColumn("ExpiresOnUtc").AsDateTime2().NotNullable()
                .WithColumn("RevokedOnUtc").AsDateTime2().Nullable();
            Create.Index("IX_MultiSmsMobileApiSession_SessionId")
                .OnTable("MultiSmsMobileApiSession").OnColumn("SessionId").Ascending().WithOptions().Unique();
            Create.Index("IX_MultiSmsMobileApiSession_CustomerId")
                .OnTable("MultiSmsMobileApiSession").OnColumn("CustomerId").Ascending();
        }
    }

    public override void Down()
    {
        // Preserve session audit/revocation state on rollback.
    }
}
