using System.Net;
using System.Net.Http;
using System.Text.Json;
using BonakMarket.ERP.Domain;
using BonakMarket.ERP.Services;

namespace BonakMarket.ERP.Infrastructure;

// Raw bounded transport. Authentication calls this directly to avoid recursion.
public sealed class ErpHttpTransport(IHttpClientFactory factory, ErpEndpointPolicy endpoints)
{
    public async Task<JsonDocument> SendAsync(ErpConnection connection, HttpRequestMessage request, CancellationToken ct)
    {
        endpoints.SameOrigin(endpoints.ValidateBase(connection.BaseUrl), request.RequestUri);
        if (connection.TimeoutSeconds is < 1 or > 120) throw new ErpIntegrationException("invalid_timeout");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(connection.TimeoutSeconds));
        using var client = factory.CreateClient("BonakMarket.ERP");
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode) throw new ErpIntegrationException("http_" + (int)response.StatusCode);
            const int limit = 8 * 1024 * 1024;
            if (response.Content.Headers.ContentLength > limit) throw new ErpIntegrationException("response_too_large");
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var buffer = new System.IO.MemoryStream();
            var bytes = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(bytes.AsMemory(), deadline.Token)) != 0)
            {
                if (buffer.Length + count > limit) throw new ErpIntegrationException("response_too_large");
                buffer.Write(bytes, 0, count);
            }
            buffer.Position = 0;
            return await JsonDocument.ParseAsync(buffer, cancellationToken: deadline.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ErpIntegrationException("timeout"); }
        catch (HttpRequestException) { throw new ErpIntegrationException("transport_error"); }
        catch (System.IO.IOException) { throw new ErpIntegrationException("transport_error"); }
        catch (JsonException) { throw new ErpIntegrationException("invalid_json"); }
    }
}
