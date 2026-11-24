using FluentMigrator;
using BonakMarket.Core.Data.Migrations;
using BonakMarket.Data.Migrations;

namespace BonakMarket.Core.Migrations;

[MigrationVersion("2026-11-24 23:00:00", "Core: Customer guild and national code")]
internal sealed class CustomerGuildMigration : Migration
{
    public override void Up()
    {
        if (!Schema.Table("Guild").Exists())
        {
            Create.Table("Guild").WithIdColumn()
                .WithColumn("Code").AsString(50).NotNullable()
                .WithColumn("Title").AsString(225).NotNullable()
                .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn("DisplayOrder").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("CreatedOnUtc").AsDateTime2().NotNullable()
                .WithColumn("UpdatedOnUtc").AsDateTime2().Nullable();
            Create.Index("UX_Guild_Code").OnTable("Guild").OnColumn("Code").Ascending().WithOptions().Unique();
        }
        if (!Schema.Table("Customer").Column("GuildId").Exists())
        {
            Create.Column("GuildId").OnTable("Customer").AsInt32().Nullable();
            Create.ForeignKey("FK_Customer_Guild").FromTable("Customer").ForeignColumn("GuildId").ToTable("Guild").PrimaryColumn("Id");
            Create.Index("IX_Customer_GuildId").OnTable("Customer").OnColumn("GuildId").Ascending();
        }
        if (!Schema.Table("Customer").Column("NationalCode").Exists())
            Create.Column("NationalCode").OnTable("Customer").AsString(10).Nullable();
    }

    public override void Down()
    {
        // Preserve customer classifications and national codes on rollback.
    }
}
