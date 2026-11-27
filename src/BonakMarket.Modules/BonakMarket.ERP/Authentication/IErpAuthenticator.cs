using System.Net.Http;
using BonakMarket.ERP.Domain;

namespace BonakMarket.ERP.Authentication;

public interface IErpAuthenticator
{
    string SystemName { get; }
    string DefaultSettingsJson { get; }
    void Validate(ErpConnection connection);
    Task AuthenticateAsync(ErpConnection connection, HttpRequestMessage request, CancellationToken ct);
    Task InvalidateAsync(ErpConnection connection, HttpRequestMessage rejectedRequest, CancellationToken ct);
}
