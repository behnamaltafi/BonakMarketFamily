using BonakMarket.ERP.Domain;

namespace BonakMarket.ERP.Contracts;
public interface IErpProviderConfiguration : IErpProvider
{
    string DefaultSettingsJson { get; }
    void Validate(ErpConnection connection);
}
