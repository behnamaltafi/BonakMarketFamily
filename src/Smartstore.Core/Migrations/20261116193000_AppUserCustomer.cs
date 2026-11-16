using System.Data;
using FluentMigrator;
using Smartstore.Core.Data.Migrations;

namespace Smartstore.Core.Migrations;

/// <summary>
/// Additive schema for independent application users and many-to-many customer access.
/// </summary>
[MigrationVersion("2026-11-16 19:30:00", "Core: Independent app users and customer memberships")]
internal class AppUserCustomer : Migration
{
    const string Users = "AppUser";
    const string Memberships = "AppUserCustomer";

    public override void Up()
    {
        if (!Schema.Table(Users).Exists())
        {
            Create.Table(Users)
                .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                .WithColumn("UserGuid").AsGuid().NotNullable()
                .WithColumn("ExternalSubject").AsString(450).Nullable()
                .WithColumn("Active").AsBoolean().NotNullable()
                .WithColumn("CreatedOnUtc").AsDateTime2().NotNullable();
        }

        if (!Schema.Table(Memberships).Exists())
        {
            Create.Table(Memberships)
                .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                .WithColumn("AppUserId").AsInt32().NotNullable()
                .WithColumn("CustomerId").AsInt32().NotNullable()
                .WithColumn("Active").AsBoolean().NotNullable();
        }

        if (!Schema.Table(Users).Index("UX_AppUser_UserGuid").Exists())
        {
            Create.Index("UX_AppUser_UserGuid").OnTable(Users)
                .OnColumn("UserGuid").Ascending().WithOptions().Unique();
        }

        if (!Schema.Table(Memberships).Index("UX_AppUserCustomer_AppUserId_CustomerId").Exists())
        {
            Create.Index("UX_AppUserCustomer_AppUserId_CustomerId").OnTable(Memberships)
                .OnColumn("AppUserId").Ascending()
                .OnColumn("CustomerId").Ascending()
                .WithOptions().Unique();
        }

        if (!Schema.Table(Memberships).Index("IX_AppUserCustomer_CustomerId").Exists())
        {
            Create.Index("IX_AppUserCustomer_CustomerId").OnTable(Memberships)
                .OnColumn("CustomerId").Ascending();
        }

        if (!Schema.Table(Memberships).Constraint("FK_AppUserCustomer_AppUser").Exists())
        {
            Create.ForeignKey("FK_AppUserCustomer_AppUser")
                .FromTable(Memberships).ForeignColumn("AppUserId")
                .ToTable(Users).PrimaryColumn("Id")
                .OnDelete(Rule.None);
        }

        if (!Schema.Table(Memberships).Constraint("FK_AppUserCustomer_Customer").Exists())
        {
            Create.ForeignKey("FK_AppUserCustomer_Customer")
                .FromTable(Memberships).ForeignColumn("CustomerId")
                .ToTable("Customer").PrimaryColumn("Id")
                .OnDelete(Rule.None);
        }
    }

    public override void Down()
    {
        if (Schema.Table(Memberships).Exists())
            Delete.Table(Memberships);

        if (Schema.Table(Users).Exists())
            Delete.Table(Users);
    }
}
