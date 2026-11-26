using System.Net.Http;
using BonakMarket.ERP.Domain;

namespace BonakMarket.ERP.Authentication;

public interface IErpAuthenticator
{
    string SystemName { get; }
    string DefaultSettingsJson { get; }
    void Validate(ErpConnection connection);
    Task AuthenticateAsync(ErpConnection connection, HttpRequestMessage request, CancellationToken ct);
    void Invalidate(ErpConnection connection, HttpRequestMessage rejectedRequest);
}
