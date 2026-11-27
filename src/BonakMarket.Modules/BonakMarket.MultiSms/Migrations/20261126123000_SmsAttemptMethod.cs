using FluentMigrator;
using BonakMarket.Core.Data.Migrations;
using BonakMarket.Data.Migrations;

namespace BonakMarket.MultiSms.Migrations;

[MigrationVersion("2026-11-26 12:30:00", "MultiSms: AttemptMethod")]
internal sealed class SmsAttemptMethod : Migration
{
    public override void Up()
    {
        if (Schema.Table("MultiSmsSendAttempt").Exists() &&
            !Schema.Table("MultiSmsSendAttempt").Column("Method").Exists())
            Alter.Table("MultiSmsSendAttempt").AddColumn("Method").AsString(50).Nullable();
    }

    public override void Down() { /* Preserve operational history. */ }
}
