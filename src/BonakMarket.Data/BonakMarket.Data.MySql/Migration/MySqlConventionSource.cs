using FluentMigrator;
using FluentMigrator.Expressions;
using FluentMigrator.Runner.Conventions;
using BonakMarket.Core.Data.Migrations;

namespace BonakMarket.Data.MySql.Migration;

internal class MySqlConventionSource : IConventionSource
{
    public void Configure(IConventionSet conventionSet)
    {
        conventionSet.ColumnsConventions.Add(new UtcDateTimeColumnsConvention());
    }
}

internal class UtcDateTimeColumnsConvention : IColumnsConvention
{
    public IColumnsExpression Apply(IColumnsExpression expression)
    {
        foreach (var column in expression.Columns)
        {
            if (column.DefaultValue.Equals(SystemMethods.CurrentUTCDateTime))
            {
                column.DefaultValue = RawSql.Insert("(UTC_TIMESTAMP)");
            }
        }

        return expression;
    }
}