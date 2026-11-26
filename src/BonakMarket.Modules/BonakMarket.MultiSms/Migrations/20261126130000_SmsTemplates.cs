using FluentMigrator;
using BonakMarket.Core.Data.Migrations;
using BonakMarket.Data.Migrations;

namespace BonakMarket.MultiSms.Migrations;

[MigrationVersion("2026-11-26 13:00:00", "MultiSms: TemplatesAndMappings")]
internal sealed class SmsTemplates : Migration
{
    public override void Up()
    {
        if (!Schema.Table("MultiSmsTemplate").Exists())
        {
            Create.Table("MultiSmsTemplate").WithIdColumn()
                .WithColumn("Key").AsString(80).NotNullable()
                .WithColumn("Title").AsString(160).NotNullable()
                .WithColumn("Parameters").AsString(2000).NotNullable()
                .WithColumn("IsOtp").AsBoolean().NotNullable()
                .WithColumn("IsActive").AsBoolean().NotNullable();
            Create.Index("IX_MultiSmsTemplate_Key").OnTable("MultiSmsTemplate").OnColumn("Key").Ascending().WithOptions().Unique();
            Insert.IntoTable("MultiSmsTemplate").Row(new { Key = "LOGINOTP", Title = "Identity OTP", Parameters = "[\"Code\"]", IsOtp = true, IsActive = true });
        }
        if (!Schema.Table("MultiSmsTemplateProvider").Exists())
        {
            Create.Table("MultiSmsTemplateProvider").WithIdColumn()
                .WithColumn("TemplateId").AsInt32().NotNullable().ForeignKey("FK_MultiSmsTemplateProvider_Template", "MultiSmsTemplate", "Id")
                .WithColumn("ProviderName").AsString(50).NotNullable()
                .WithColumn("ExternalTemplateId").AsString(100).NotNullable()
                .WithColumn("ParameterMapping").AsString(4000).NotNullable();
            Create.Index("IX_MultiSmsTemplateProvider_Template_Provider").OnTable("MultiSmsTemplateProvider")
                .OnColumn("TemplateId").Ascending().OnColumn("ProviderName").Ascending().WithOptions().Unique();
        }
        if (!Schema.Table("MultiSmsMessage").Column("DeliveryMode").Exists())
            Alter.Table("MultiSmsMessage").AddColumn("DeliveryMode").AsString(20).Nullable();
        if (!Schema.Table("MultiSmsMessage").Column("TemplateKey").Exists())
            Alter.Table("MultiSmsMessage").AddColumn("TemplateKey").AsString(80).Nullable();
    }

    public override void Down() { /* Keep templates and history across rollback. */ }
}
