using FluentMigrator;
using BonakMarket.Core.Data.Migrations;
using BonakMarket.Data.Migrations;

namespace BonakMarket.ERP.Migrations;
[MigrationVersion("2026-11-26 11:30:00", "ERP: Separate provider and authentication settings")]
internal sealed class ConnectionSettings : Migration
{
    public override void Up()
    {
        Alter.Table("ErpConnection")
            .AddColumn("AuthenticationType").AsString(80).NotNullable().WithDefaultValue("VaranegarLogin")
            .AddColumn("ProviderSettingsJson").AsString(int.MaxValue).Nullable()
            .AddColumn("AuthenticationSettingsJson").AsString(int.MaxValue).Nullable()
            .AddColumn("EncryptedAuthenticationSettings").AsString(int.MaxValue).Nullable()
            .AddColumn("LegacyLoginPayload").AsBoolean().NotNullable().WithDefaultValue(false)
            .AddColumn("ConfigurationVersion").AsGuid().NotNullable().WithDefaultValue(SystemMethods.NewGuid);
        Execute.Sql("""
            UPDATE c SET
                AuthenticationType = CASE WHEN UseLogin = 1 THEN 'VaranegarLogin' ELSE 'None' END,
                EncryptedAuthenticationSettings = EncryptedLoginBody,
                LegacyLoginPayload = CASE WHEN EncryptedLoginBody IS NULL THEN 0 ELSE 1 END,
                ProviderSettingsJson = (SELECT c.ODataPrefix, c.ProductsPath, c.PricesPath,
                    c.InventoryPath, c.CategoriesPath, c.WarehousesPath FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
                AuthenticationSettingsJson = (SELECT c.LoginPath, COALESCE(c.AuthorizationScheme, '') AS AuthorizationScheme FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
            FROM ErpConnection c
            """);
        foreach (var column in new[] { "ODataPrefix", "LoginPath", "AuthorizationScheme", "ProductsPath", "PricesPath", "InventoryPath",
            "CategoriesPath", "WarehousesPath", "UseLogin", "EncryptedLoginBody" })
            Delete.Column(column).FromTable("ErpConnection");
    }
    public override void Down() { /* Forward-only; preserve credentials and mappings. */ }
}
