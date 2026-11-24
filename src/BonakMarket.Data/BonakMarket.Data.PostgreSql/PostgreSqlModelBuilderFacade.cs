#nullable enable

using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BonakMarket.Data.Providers;

namespace BonakMarket.Data.PostgreSql;

internal class PostgreSqlModelBuilderFacade : UnifiedModelBuilderFacade
{
    public override bool CanSetIncludeProperties(IConventionIndexBuilder indexBuilder, IReadOnlyList<string>? propertyNames)
    {
        return NpgsqlIndexBuilderExtensions.CanSetIncludeProperties(indexBuilder, propertyNames, false);
    }

    public override void IncludeIndexProperties(IndexBuilder indexBuilder, params string[] propertyNames)
    {
        NpgsqlIndexBuilderExtensions.IncludeProperties(indexBuilder, propertyNames);
    }

    public override void UseKeySequences(ModelBuilder modelBuilder, string? nameSuffix = null, string? schema = null)
    {
        NpgsqlModelBuilderExtensions.UseKeySequences(modelBuilder, nameSuffix, schema);
    }
}