using BonakMarket.ERP.Services;

namespace BonakMarket.ERP.Authentication;
public sealed class ErpAuthenticationResolver(IEnumerable<IErpAuthenticator> authenticators)
{
    public string[] Names => authenticators.Select(x => x.SystemName).ToArray();
    public IErpAuthenticator Resolve(string name) => authenticators.SingleOrDefault(
        x => string.Equals(x.SystemName, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ErpIntegrationException("unsupported_authentication");
}
public sealed class NoAuthentication : IErpAuthenticator
{
    public string SystemName => "None";
    public string DefaultSettingsJson => "{}";
    public void Validate(BonakMarket.ERP.Domain.ErpConnection connection) { }
    public Task AuthenticateAsync(BonakMarket.ERP.Domain.ErpConnection connection, System.Net.Http.HttpRequestMessage request, CancellationToken ct)
        => Task.CompletedTask;
    public void Invalidate(BonakMarket.ERP.Domain.ErpConnection connection, System.Net.Http.HttpRequestMessage rejectedRequest) { }
}
