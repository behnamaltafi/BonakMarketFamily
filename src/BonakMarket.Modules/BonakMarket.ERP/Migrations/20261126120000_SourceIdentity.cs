using FluentMigrator;
using BonakMarket.Core.Data.Migrations;
using BonakMarket.Data.Migrations;

namespace BonakMarket.ERP.Migrations;
[MigrationVersion("2026-11-26 12:00:00", "ERP: Stable external source identity")]
internal sealed class SourceIdentity : Migration
{
    public override void Up()
    {
        Alter.Table("ErpConnection").AddColumn("SourceCode").AsString(80).NotNullable().WithDefaultValue("");
        Alter.Table("ErpCatalogProduct").AddColumn("SourceCode").AsString(80).NotNullable().WithDefaultValue("");
        Execute.Sql("UPDATE ErpConnection SET SourceCode = Code");
        Execute.Sql("UPDATE p SET SourceCode = c.SourceCode FROM ErpCatalogProduct p INNER JOIN ErpConnection c ON c.Id = p.ConnectionId");
        Delete.Index("UX_ErpCatalogProduct_External").OnTable("ErpCatalogProduct");
        Create.Index("UX_ErpCatalogProduct_External").OnTable("ErpCatalogProduct")
            .OnColumn("CompanyCode").Ascending().OnColumn("SourceCode").Ascending().OnColumn("ExternalId").Ascending().WithOptions().Unique();
    }
    public override void Down() { /* Keep mappings. */ }
}
