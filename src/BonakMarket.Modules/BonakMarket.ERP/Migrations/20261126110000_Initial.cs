using FluentMigrator;
using BonakMarket.Core.Data.Migrations;
using BonakMarket.Data.Migrations;

namespace BonakMarket.ERP.Migrations;
[MigrationVersion("2026-11-26 11:00:00", "ERP: Initial catalog integration")]
internal sealed class Initial : Migration
{
    public override void Up()
    {
        Create.Table("ErpConnection").WithIdColumn()
            .WithColumn("CompanyCode").AsString(80).NotNullable()
            .WithColumn("Code").AsString(80).NotNullable()
            .WithColumn("Name").AsString(150).NotNullable()
            .WithColumn("Provider").AsString(80).NotNullable()
            .WithColumn("BaseUrl").AsString(500).NotNullable()
            .WithColumn("Enabled").AsBoolean().NotNullable()
            .WithColumn("TimeoutSeconds").AsInt32().NotNullable()
            .WithColumn("ODataPrefix").AsString(200).NotNullable()
            .WithColumn("LoginPath").AsString(200).NotNullable()
            .WithColumn("AuthorizationScheme").AsString(40).Nullable()
            .WithColumn("UseLogin").AsBoolean().NotNullable()
            .WithColumn("EncryptedLoginBody").AsString(int.MaxValue).Nullable();
        Create.Index("UX_ErpConnection_Company_Code").OnTable("ErpConnection")
            .OnColumn("CompanyCode").Ascending().OnColumn("Code").Ascending().WithOptions().Unique();

        Create.Table("ErpCatalogProduct").WithIdColumn()
            .WithColumn("CompanyCode").AsString(80).NotNullable()
            .WithColumn("ExternalId").AsString(150).NotNullable()
            .WithColumn("ConnectionId").AsInt32().NotNullable()
            .WithColumn("ProductId").AsInt32().Nullable()
            .WithColumn("Sku").AsString(400).NotNullable()
            .WithColumn("Name").AsString(400).NotNullable()
            .WithColumn("ActiveForSale").AsBoolean().NotNullable()
            .WithColumn("SnapshotJson").AsString(int.MaxValue).Nullable()
            .WithColumn("LastSeenUtc").AsDateTime2().NotNullable();
        Create.Index("UX_ErpCatalogProduct_External").OnTable("ErpCatalogProduct")
            .OnColumn("CompanyCode").Ascending().OnColumn("ExternalId").Ascending().WithOptions().Unique();
        // SQL Server: a normal unique index allows only one NULL.
        Execute.Sql("CREATE UNIQUE INDEX [UX_ErpCatalogProduct_Local] ON [ErpCatalogProduct] ([ProductId]) WHERE [ProductId] IS NOT NULL");
        Create.ForeignKey("FK_ErpCatalogProduct_Connection").FromTable("ErpCatalogProduct").ForeignColumn("ConnectionId").ToTable("ErpConnection").PrimaryColumn("Id");

        Create.Table("ErpSyncRun").WithIdColumn()
            .WithColumn("ConnectionId").AsInt32().NotNullable()
            .WithColumn("Operation").AsString(40).NotNullable()
            .WithColumn("Status").AsString(30).NotNullable()
            .WithColumn("StartedUtc").AsDateTime2().NotNullable()
            .WithColumn("FinishedUtc").AsDateTime2().Nullable()
            .WithColumn("Rows").AsInt32().NotNullable()
            .WithColumn("ErrorCode").AsString(100).Nullable();
        Create.Index("IX_ErpSyncRun_Connection").OnTable("ErpSyncRun")
            .OnColumn("ConnectionId").Ascending().OnColumn("StartedUtc").Ascending();
    }

    public override void Down()
    {
        // Retain integration mappings and operational history during rollback.
    }
}
