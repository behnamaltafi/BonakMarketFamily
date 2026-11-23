using FluentMigrator.Runner.Conventions;

namespace BonakMarket.Core.Data.Migrations;

public interface IConventionSource
{
    void Configure(IConventionSet conventionSet);
}