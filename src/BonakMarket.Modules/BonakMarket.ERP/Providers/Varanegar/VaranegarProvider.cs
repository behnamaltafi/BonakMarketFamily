using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BonakMarket.ERP.Contracts;
using BonakMarket.ERP.Domain;
using BonakMarket.ERP.Services;

namespace BonakMarket.ERP.Providers.Varanegar;

public sealed class VaranegarProvider(IHttpClientFactory factory, ErpSecrets secrets) :
    IErpProductProvider, IErpPriceProvider, IErpInventoryProvider, IErpClassificationProvider
{
    public string SystemName => "Varanegar";
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public async Task<ErpPage<ErpProduct>> GetProductsAsync(ErpConnection c, int skip, int take, CancellationToken ct)
        => new((await Read<ProductRow>(c, c.ProductsPath, skip, take, ct))
            .Select(x => new ErpProduct(Id(x.Id), Required(x.GoodsCode), Required(x.GoodsName),
                Flag(x.NoSaleFlag), Flag(x.NoStockFlag), OptionalId(x.BrandRef), x.BrandName,
                OptionalId(x.ManufacturerRef), x.ManufacturerName, OptionalId(x.GoodsGroupRef),
                x.UnitName, x.PackUnitName, x.CartonType, x.DefaultBarcode ?? x.Barcode1, x.GTIN,
                ValidPrice(x.SalePrice), ValidPrice(x.UserPrice), ValidPrice(x.ManufacturerPrice), x.Tax, x.Charge)).ToArray());

    public async Task<ErpPage<ErpPrice>> GetPricesAsync(ErpConnection c, int skip, int take, CancellationToken ct)
        => new((await Read<PriceRow>(c, c.PricesPath, skip, take, ct))
            .Select(x => new ErpPrice(Id(x.Id), Id(x.GoodsRef), ValidPrice(x.SalePrice),
                ValidPrice(x.UserPrice), ValidPrice(x.ManufacturerPrice), x.StartDate, x.EndDate,
                OptionalId(x.DCRef), OptionalId(x.CustRef), OptionalId(x.CustCtgrRef), OptionalId(x.CustActRef),
                OptionalId(x.CustLevelRef), OptionalId(x.PaymentTypeRef), OptionalId(x.PaymentUsanceRef),
                OptionalId(x.OrderTypeRef), OptionalId(x.StateRef), OptionalId(x.CountyRef), OptionalId(x.AreaRef),
                OptionalId(x.MainCustTypeRef), OptionalId(x.SubCustTypeRef), x.Code)).ToArray());

    public async Task<ErpPage<ErpStock>> GetInventoryAsync(ErpConnection c, int skip, int take, CancellationToken ct)
        => new((await Read<StockRow>(c, c.InventoryPath, skip, take, ct))
            .Select(x => new ErpStock(Id(x.Id), Id(x.GoodsRef),
                x.StockDCRef is >= 0 ? x.StockDCRef.Value.ToString(CultureInfo.InvariantCulture) : throw new ErpIntegrationException("invalid_warehouse"),
                x.OnHandQty ?? throw new ErpIntegrationException("missing_quantity"),
                x.CartonType, x.IsBatch ?? throw new ErpIntegrationException("missing_batch_flag"),
                ValidPrice(x.SalePrice))).ToArray());

    public async Task<ErpPage<ErpCategory>> GetCategoriesAsync(ErpConnection c, int skip, int take, CancellationToken ct)
        => new((await Read<CategoryRow>(c, c.CategoriesPath, skip, take, ct))
            .Select(x => new ErpCategory(Id(x.Id), OptionalId(x.ParentId), Required(x.GoodsGroupName), x.GoodsGroupPath)).ToArray());

    public async Task<ErpPage<ErpWarehouse>> GetWarehousesAsync(ErpConnection c, int skip, int take, CancellationToken ct)
        => new((await Read<WarehouseRow>(c, c.WarehousesPath, skip, take, ct))
            .Select(x => new ErpWarehouse(Id(x.Id), x.StockDCCode, Required(x.StockDCName), OptionalId(x.DCRef))).ToArray());

    public static Uri ValidateBase(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ErpIntegrationException("invalid_https_base_url");
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
    }
    public static Uri Relative(Uri root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith("//", StringComparison.Ordinal) ||
            path.Contains('\\') || path.Contains('?') || path.Contains('#') || path.Contains("..", StringComparison.Ordinal) ||
            path.Contains('%') || path.Any(char.IsControl) || Uri.TryCreate(path.TrimStart('/'), UriKind.Absolute, out _))
            throw new ErpIntegrationException("invalid_relative_path");
        return SameOrigin(root, new Uri(root, path.TrimStart('/')));
    }
    private static Uri SameOrigin(Uri root, Uri uri)
    {
        if (uri.Scheme != root.Scheme || uri.IdnHost != root.IdnHost || uri.Port != root.Port ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment) ||
            !uri.AbsolutePath.StartsWith(root.AbsolutePath, StringComparison.Ordinal))
            throw new ErpIntegrationException("unexpected_destination");
        return uri;
    }

    private async Task<List<T>> Read<T>(ErpConnection c, string entity, int skip, int take, CancellationToken ct)
    {
        if (!c.Enabled) throw new ErpIntegrationException("connection_disabled");
        if (skip < 0 || take is < 1 or > 100 || c.TimeoutSeconds is < 1 or > 120)
            throw new ErpIntegrationException("invalid_page_or_timeout");
        var root = ValidateBase(c.BaseUrl);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(c.TimeoutSeconds));
        var token = timeout.Token;
        using var client = factory.CreateClient("BonakMarket.ERP");
        try
        {
            // Login is performed per bounded operation, avoiding token mixups across companies/fiscal years.
            var authorization = c.UseLogin ? await Login(client, root, c, token) : null;
            var prefix = c.ODataPrefix?.Trim('/');
            var endpoint = Relative(root, string.IsNullOrEmpty(prefix) ? entity : prefix + "/" + entity);
            Uri next = new UriBuilder(endpoint) { Query = "$orderby=Id&$skip=" + skip.ToString(CultureInfo.InvariantCulture) + "&$top=" + take.ToString(CultureInfo.InvariantCulture) }.Uri;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var rows = new List<T>();
            while (next != null)
            {
                if (!seen.Add(next.AbsoluteUri) || seen.Count > 100) throw new ErpIntegrationException("pagination_loop");
                using var request = new HttpRequestMessage(HttpMethod.Get, next);
                if (authorization != null && !request.Headers.TryAddWithoutValidation("Authorization", authorization))
                    throw new ErpIntegrationException("invalid_authorization");
                using var doc = await Send(client, request, token);
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
                next = null;
                if (envelope.ValueKind == JsonValueKind.Object &&
                    (envelope.TryGetProperty("@odata.nextLink", out var link) || envelope.TryGetProperty("odata.nextLink", out link)) &&
                    link.ValueKind != JsonValueKind.Null)
                {
                    if (link.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(link.GetString()) || rows.Count == take)
                        throw new ErpIntegrationException("invalid_next_link");
                    next = SameOrigin(root, new Uri(request.RequestUri, link.GetString()));
                }
            }
            return rows;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ErpIntegrationException("timeout"); }
        catch (HttpRequestException) { throw new ErpIntegrationException("transport_error"); }
        catch (JsonException) { throw new ErpIntegrationException("invalid_json"); }
        catch (UriFormatException) { throw new ErpIntegrationException("invalid_uri"); }
        catch (FormatException) { throw new ErpIntegrationException("invalid_response_format"); }
        catch (System.Security.Cryptography.CryptographicException) { throw new ErpIntegrationException("credential_decryption_failed"); }
    }

    private async Task<string> Login(HttpClient client, Uri root, ErpConnection c, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(c.EncryptedLoginBody)) throw new ErpIntegrationException("login_not_configured");
        using var request = new HttpRequestMessage(HttpMethod.Post, Relative(root, c.LoginPath))
        {
            Content = new StringContent(secrets.Unprotect(c.EncryptedLoginBody), Encoding.UTF8, "application/json")
        };
        using var doc = await Send(client, request, ct);
        var login = doc.RootElement.Deserialize<LoginResponse>(Json);
        if (login == null || string.IsNullOrWhiteSpace(login.Token) || login.ExpiresAt == null ||
            login.Token.Any(char.IsControl) || login.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new ErpIntegrationException("invalid_login_response");
        if (string.IsNullOrWhiteSpace(c.AuthorizationScheme)) return login.Token;
        return new AuthenticationHeaderValue(c.AuthorizationScheme, login.Token).ToString();
    }

    private static async Task<JsonDocument> Send(HttpClient client, HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) throw new ErpIntegrationException("http_" + (int)response.StatusCode);
        const int limit = 8 * 1024 * 1024;
        if (response.Content.Headers.ContentLength > limit) throw new ErpIntegrationException("response_too_large");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new System.IO.MemoryStream();
        var bytes = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(bytes.AsMemory(), ct)) != 0)
        {
            if (buffer.Length + count > limit) throw new ErpIntegrationException("response_too_large");
            buffer.Write(bytes, 0, count);
        }
        buffer.Position = 0;
        return await JsonDocument.ParseAsync(buffer, cancellationToken: ct);
    }

    private sealed class LoginResponse
    {
        public LoginResponse() { }
        public string Token { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
    }
    private static string Required(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 400
        ? value : throw new ErpIntegrationException("invalid_required_text");
    private static string Id(int? value) => value is > 0 ? value.Value.ToString(CultureInfo.InvariantCulture)
        : throw new ErpIntegrationException("invalid_external_id");
    private static string OptionalId(int? value) => value?.ToString(CultureInfo.InvariantCulture);
    private static bool Flag(int? value) => value switch { 0 => true, 1 => false, _ => throw new ErpIntegrationException("invalid_activity_flag") };
    private static decimal? ValidPrice(decimal? value) => value is >= 0 ? value : null;
}
