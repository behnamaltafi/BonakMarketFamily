using System.Net.Http;
using System.Text.Json;
using BonakMarket.ERP.Authentication;
using BonakMarket.ERP.Domain;
using BonakMarket.ERP.Services;

namespace BonakMarket.ERP.Infrastructure;

public sealed class ErpHttpClient(ErpHttpTransport transport, ErpEndpointPolicy endpoints, ErpAuthenticationResolver authentication)
{
    // Read only: retry a rejected token once, never replay a write here.
    public async Task<JsonDocument> GetAsync(ErpConnection connection, Uri uri, CancellationToken ct)
    {
        if (!connection.Enabled) throw new ErpIntegrationException("connection_disabled");
        endpoints.SameOrigin(endpoints.ValidateBase(connection.BaseUrl), uri);
        var auth = authentication.Resolve(connection.AuthenticationType);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        await auth.AuthenticateAsync(connection, request, ct);
        try { return await transport.SendAsync(connection, request, ct); }
        catch (ErpIntegrationException ex) when (ex.Code == "http_401" && auth.SystemName != "None")
        {
            await auth.InvalidateAsync(connection, request, ct);
            using var retry = new HttpRequestMessage(HttpMethod.Get, uri);
            await auth.AuthenticateAsync(connection, retry, ct);
            return await transport.SendAsync(connection, retry, ct);
        }
    }
}
