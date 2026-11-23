using System.Threading;
using FluentMigrator;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Data;
using BonakMarket.Core.Data.Migrations;
using BonakMarket.Data.Migrations;
using BonakMarket.Utilities;

namespace BonakMarket.Google.Analytics.Migrations;

[MigrationVersion("2026-09-21 12:00:00", "GoogleAnalytics: Item identifier")]
internal class ItemIdentifier : Migration, IDataSeeder<SmartDbContext>
{
    public override void Up()
    {
    }

    public override void Down()
    {
    }

    public DataSeederStage Stage => DataSeederStage.Early;
    public bool AbortOnFailure => false;

    public async Task SeedAsync(SmartDbContext context, CancellationToken cancelToken = default)
    {
        // Existing installations keep the SKU as item_id, otherwise their item statistics in Google Analytics would break.
        // New installations get the default value of GoogleAnalyticsSettings.ItemIdentifier.
        var isInstalled = await context.Settings.AnyAsync(x => x.Name.StartsWith(nameof(GoogleAnalyticsSettings) + "."), cancelToken);
        if (isInstalled)
        {
            await context.MigrateSettingsAsync(builder =>
            {
                // Add is skipped if the setting already exists.
                builder.Add(TypeHelper.NameOf<GoogleAnalyticsSettings>(x => x.ItemIdentifier, true), AnalyticsItemIdentifier.Sku.ToString());
            });
        }
    }
}
