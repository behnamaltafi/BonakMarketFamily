using BonakMarket.ERP.Contracts;

namespace BonakMarket.ERP.Services;
public sealed class ErpProviderResolver(IEnumerable<IErpProvider> providers)
{
    public string[] Names => providers.Select(x => x.SystemName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public T Resolve<T>(string name) where T : class, IErpProvider
        => providers.OfType<T>().SingleOrDefault(x => string.Equals(x.SystemName, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new ErpIntegrationException("unsupported_provider_operation");
}
public sealed class ErpIntegrationException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
