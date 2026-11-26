using FluentMigrator;
using BonakMarket.Core.Data.Migrations;
using BonakMarket.Data.Migrations;

namespace BonakMarket.ERP.Migrations;
[MigrationVersion("2026-11-26 11:45:00", "ERP: Operation routing and synchronization state")]
internal sealed class Operations : Migration
{
    public override void Up()
    {
        Create.Table("ErpOperationConfiguration").WithIdColumn()
            .WithColumn("CompanyCode").AsString(80).NotNullable()
            .WithColumn("Operation").AsString(40).NotNullable()
            .WithColumn("ConnectionId").AsInt32().NotNullable()
            .WithColumn("Enabled").AsBoolean().NotNullable()
            .WithColumn("ScheduleEnabled").AsBoolean().NotNullable()
            .WithColumn("IntervalMinutes").AsInt32().NotNullable()
            .WithColumn("BatchSize").AsInt32().NotNullable()
            .WithColumn("NextOffset").AsInt32().NotNullable()
            .WithColumn("NextRunUtc").AsDateTime2().Nullable()
            .WithColumn("LastSucceededUtc").AsDateTime2().Nullable()
            .WithColumn("Revision").AsGuid().NotNullable();
        Create.Index("UX_ErpOperation_Company_Operation").OnTable("ErpOperationConfiguration")
            .OnColumn("CompanyCode").Ascending().OnColumn("Operation").Ascending().WithOptions().Unique();
        Create.ForeignKey("FK_ErpOperation_Connection").FromTable("ErpOperationConfiguration").ForeignColumn("ConnectionId").ToTable("ErpConnection").PrimaryColumn("Id");
        Alter.Table("ErpSyncRun").AddColumn("CorrelationId").AsGuid().Nullable()
            .AddColumn("Offset").AsInt32().NotNullable().WithDefaultValue(0);
    }
    public override void Down() { /* Preserve operational data. */ }
}
