using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using BonakMarket.ERP.Domain;
using BonakMarket.ERP.Infrastructure;
using BonakMarket.ERP.Services;

namespace BonakMarket.ERP.Providers.Varanegar;
public sealed class VaranegarODataClient(ErpHttpClient http, ErpEndpointPolicy endpoints)
{
    private static readonly JsonSerializerOptions Json = new()
    { PropertyNameCaseInsensitive = true, NumberHandling = JsonNumberHandling.AllowReadingFromString };

    public async Task<List<T>> ReadAsync<T>(ErpConnection c, string entity, int skip, int take, CancellationToken ct)
    {
        if (!c.Enabled) throw new ErpIntegrationException("connection_disabled");
        if (skip < 0 || take is < 1 or > 100 || c.TimeoutSeconds is < 1 or > 120)
            throw new ErpIntegrationException("invalid_page_or_timeout");
        var root = endpoints.ValidateBase(c.BaseUrl);
        var prefix = ErpSettingsJson.Read<VaranegarSettings>(c.ProviderSettingsJson).ODataPrefix?.Trim('/');
        var endpoint = endpoints.Relative(root, string.IsNullOrEmpty(prefix) ? entity : prefix + "/" + entity);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(c.TimeoutSeconds));
        var token = timeout.Token;
        try
        {
            Uri next = new UriBuilder(endpoint) { Query = "$orderby=Id&$skip=" + skip.ToString(CultureInfo.InvariantCulture) + "&$top=" + take.ToString(CultureInfo.InvariantCulture) }.Uri;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var rows = new List<T>();
            while (next != null)
            {
                if (!seen.Add(next.AbsoluteUri) || seen.Count > 100) throw new ErpIntegrationException("pagination_loop");
                using var doc = await http.GetAsync(c, next, token);
                var envelope = doc.RootElement;
                JsonElement values;
                if (envelope.ValueKind == JsonValueKind.Array) values = envelope;
                else if (envelope.ValueKind == JsonValueKind.Object && envelope.TryGetProperty("value", out var list) && list.ValueKind == JsonValueKind.Array) values = list;
                else throw new ErpIntegrationException("unexpected_odata_envelope");
                foreach (var row in values.EnumerateArray())
                {
                    if (rows.Count == take) throw new ErpIntegrationException("page_limit_exceeded");
                    rows.Add(row.Deserialize<T>(Json) ?? throw new ErpIntegrationException("invalid_row"));
                }
                var previous = next;
                next = null;
                if (envelope.ValueKind == JsonValueKind.Object &&
                    (envelope.TryGetProperty("@odata.nextLink", out var link) || envelope.TryGetProperty("odata.nextLink", out link)) &&
                    link.ValueKind != JsonValueKind.Null)
                {
                    if (link.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(link.GetString()))
                        throw new ErpIntegrationException("invalid_next_link");
                    if (rows.Count < take) next = endpoints.SameOrigin(root, new Uri(previous, link.GetString()));
                }
            }
            return rows;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ErpIntegrationException("timeout"); }
        catch (JsonException) { throw new ErpIntegrationException("invalid_json"); }
        catch (FormatException) { throw new ErpIntegrationException("invalid_response_format"); }
    }
}
