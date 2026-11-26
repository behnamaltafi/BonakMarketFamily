using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using BonakMarket.ERP.Authentication;
using BonakMarket.ERP.Infrastructure;
using NUnit.Framework;
using BonakMarket.ERP.Domain;
using BonakMarket.ERP.Contracts;
using BonakMarket.ERP.Providers.Varanegar;
using BonakMarket.ERP.Services;

namespace BonakMarket.Core.Tests.ERP;

[TestFixture]
public class VaranegarProviderTests
{
    private static ErpConnection Connection() => new()
    {
        BaseUrl = "https://erp.example.test/thirdparty/",
        Enabled = true,
        AuthenticationType = "None"
    };

    [Test]
    public async Task Product_mapping_preserves_identity_units_and_activity()
    {
        using var handler = new FakeHandler(_ => Reply("""
            {"value":[{"Id":125,"GoodsCode":"00125","GoodsName":"Oil","NoSaleFlag":1,"NoStockFlag":0,"CartonType":12,"SalePrice":1000,"UserPrice":1200}]}
            """));
        var provider = Provider(handler);
        var page = await provider.GetProductsAsync(Connection(), 20, 10, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(page.Items[0].ExternalId, Is.EqualTo("125"));
            Assert.That(page.Items[0].Sku, Is.EqualTo("00125"));
            Assert.That(page.Items[0].ActiveForSale, Is.False);
            Assert.That(page.Items[0].ActiveForStock, Is.True);
            Assert.That(page.Items[0].UnitsPerPack, Is.EqualTo(12m));
            Assert.That(page.Items[0].PublicSalePrice, Is.EqualTo(1000m));
            Assert.That(handler.Requests[0].AbsolutePath, Does.EndWith("/odata/ThirdPartyProductInfoOData"));
            Assert.That(handler.Requests[0].Query, Does.Contain("$skip=20"));
        });
    }

    [Test]
    public async Task Stock_aggregate_and_negative_price_are_not_misinterpreted()
    {
        using var handler = new FakeHandler(_ => Reply("""
            {"value":[{"Id":1,"GoodsRef":125,"StockDCRef":0,"OnHandQty":12.5,"CartonType":6,"IsBatch":true,"SalePrice":-1}]}
            """));
        var page = await Provider(handler).GetInventoryAsync(Connection(), 0, 10, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(page.Items[0].WarehouseId, Is.Null);
            Assert.That(page.Items[0].IsAggregate, Is.True);
            Assert.That(page.Items[0].AvailableQuantity, Is.EqualTo(12.5m));
            Assert.That(page.Items[0].PublicSalePrice, Is.Null);
            Assert.That(page.Items[0].IsBatch, Is.True);
        });
    }

    [Test]
    public async Task Contract_price_retains_customer_distribution_and_payment_scope()
    {
        using var handler = new FakeHandler(_ => Reply("""
            {"value":[{"Id":1,"GoodsRef":125,"CustRef":15,"DCRef":2,"PaymentTypeRef":3,"Code":4,"SalePrice":123,"StartDate":"1402/01/01"}]}
            """));
        var page = await Provider(handler).GetPricesAsync(Connection(), 0, 10, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(page.Items[0].CustomerId, Is.EqualTo("15"));
            Assert.That(page.Items[0].DistributionId, Is.EqualTo("2"));
            Assert.That(page.Items[0].PaymentTypeId, Is.EqualTo("3"));
            Assert.That(page.Items[0].StartDate, Is.EqualTo("1402/01/01"));
        });
    }

    [Test]
    public void Cross_origin_next_link_is_rejected_before_sending()
    {
        using var handler = new FakeHandler(_ => Reply("""
            {"value":[],"@odata.nextLink":"https://other.example.test/steal"}
            """));
        var exception = Assert.ThrowsAsync<ErpIntegrationException>(async () =>
            await Provider(handler).GetProductsAsync(Connection(), 0, 10, CancellationToken.None));
        Assert.That(exception.Code, Is.EqualTo("unexpected_destination"));
        Assert.That(handler.Requests, Has.Count.EqualTo(1));
    }

    [Test]
    public void Missing_stock_quantity_does_not_become_zero()
    {
        using var handler = new FakeHandler(_ => Reply("""
            {"value":[{"Id":1,"GoodsRef":125,"StockDCRef":0,"IsBatch":false}]}
            """));
        var exception = Assert.ThrowsAsync<ErpIntegrationException>(async () =>
            await Provider(handler).GetInventoryAsync(Connection(), 0, 10, CancellationToken.None));
        Assert.That(exception.Code, Is.EqualTo("missing_quantity"));
    }

    [Test]
    public async Task Login_uses_configured_body_and_authorizes_read_without_persisting_token()
    {
        var secrets = new ErpSecrets(new EphemeralDataProtectionProvider());
        var c = Connection();
        c.AuthenticationType = "VaranegarLogin";
        c.EncryptedAuthenticationSettings = secrets.Protect(ErpSettingsJson.Write(new ErpUsernamePassword { Username = "sample", Password = "secret" }));
        var received = false;
        using var handler = new FakeHandler(request =>
        {
            if (request.Method == HttpMethod.Post)
                return Reply("{\"Token\":\"test-token\",\"expiresAt\":\"2099-01-01T00:00:00Z\"}");
            received = request.Headers.Authorization?.ToString() == "Bearer test-token";
            return Reply("{\"value\":[]}");
        });
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var provider = Provider(handler, secrets, memory);
        await provider.GetProductsAsync(c, 0, 10, CancellationToken.None);
        await provider.GetProductsAsync(c, 0, 10, CancellationToken.None);
        Assert.That(received, Is.True);
        Assert.That(handler.Requests, Has.Count.EqualTo(3));
        Assert.That(handler.Requests[0].AbsolutePath, Does.EndWith("/api/auth/login"));
    }

    [Test]
    public async Task Unauthorized_read_refreshes_token_once()
    {
        var secrets = new ErpSecrets(new EphemeralDataProtectionProvider());
        var c = Connection();
        c.AuthenticationType = "VaranegarLogin";
        c.EncryptedAuthenticationSettings = secrets.Protect(ErpSettingsJson.Write(new ErpUsernamePassword { Username = "user", Password = "password" }));
        var logins = 0;
        var reads = 0;
        using var handler = new FakeHandler(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                logins++;
                return Reply("{\"Token\":\"token-" + logins + "\",\"expiresAt\":\"2099-01-01T00:00:00Z\"}");
            }
            reads++;
            if (reads == 1) return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            Assert.That(request.Headers.Authorization?.ToString(), Is.EqualTo("Bearer token-2"));
            return Reply("{\"value\":[]}");
        });
        using var memory = new MemoryCache(new MemoryCacheOptions());
        await Provider(handler, secrets, memory).GetProductsAsync(c, 0, 10, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(logins, Is.EqualTo(2));
            Assert.That(reads, Is.EqualTo(2));
        });
    }

    [Test]
    public void Unauthorized_read_never_replays_more_than_once()
    {
        var secrets = new ErpSecrets(new EphemeralDataProtectionProvider());
        var c = Connection();
        c.AuthenticationType = "VaranegarLogin";
        c.EncryptedAuthenticationSettings = secrets.Protect(ErpSettingsJson.Write(new ErpUsernamePassword { Username = "user", Password = "password" }));
        using var handler = new FakeHandler(request => request.Method == HttpMethod.Post
            ? Reply("{\"Token\":\"test-token\",\"expiresAt\":\"2099-01-01T00:00:00Z\"}")
            : new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var error = Assert.ThrowsAsync<ErpIntegrationException>(async () =>
            await Provider(handler, secrets, memory).GetProductsAsync(c, 0, 10, CancellationToken.None));
        Assert.That(error.Code, Is.EqualTo("http_401"));
        Assert.That(handler.Requests, Has.Count.EqualTo(4));
    }

    [Test]
    public async Task Connection_settings_version_invalidates_cached_token()
    {
        var secrets = new ErpSecrets(new EphemeralDataProtectionProvider());
        var c = Connection();
        c.AuthenticationType = "VaranegarLogin";
        c.EncryptedAuthenticationSettings = secrets.Protect(ErpSettingsJson.Write(new ErpUsernamePassword { Username = "user", Password = "password" }));
        var logins = 0;
        using var handler = new FakeHandler(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                logins++;
                return Reply("{\"Token\":\"test-token\",\"expiresAt\":\"2099-01-01T00:00:00Z\"}");
            }
            return Reply("{\"value\":[]}");
        });
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var provider = Provider(handler, secrets, memory);
        await provider.GetProductsAsync(c, 0, 10, CancellationToken.None);
        c.ConfigurationVersion = Guid.NewGuid();
        await provider.GetProductsAsync(c, 0, 10, CancellationToken.None);
        Assert.That(logins, Is.EqualTo(2));
    }

    [Test]
    public void Unlisted_destination_is_blocked_before_login_or_read()
    {
        using var handler = new FakeHandler(_ => Reply("{\"value\":[]}"));
        var c = Connection();
        c.BaseUrl = "https://not-approved.example.test/";
        var error = Assert.ThrowsAsync<ErpIntegrationException>(async () =>
            await Provider(handler).GetProductsAsync(c, 0, 10, CancellationToken.None));
        Assert.That(error.Code, Is.EqualTo("erp_destination_not_allowed"));
        Assert.That(handler.Requests, Is.Empty);
    }

    [Test]
    public void Unsupported_schedule_and_cross_company_route_are_rejected()
    {
        using var handler = new FakeHandler(_ => Reply("{\"value\":[]}"));
        var provider = Provider(handler);
        var router = new ErpOperationRouter(null, new ErpProviderResolver(new IErpProvider[] { provider }));
        var c = Connection();
        c.CompanyCode = "company-a";
        c.Provider = "Varanegar";
        var route = new ErpOperationConfiguration { CompanyCode = "company-b", Operation = "Products" };
        Assert.That(Assert.Throws<ErpIntegrationException>(() => router.Validate(route, c)).Code, Is.EqualTo("connection_company_mismatch"));
        route.CompanyCode = c.CompanyCode;
        route.Operation = "Prices";
        route.ScheduleEnabled = true;
        Assert.That(Assert.Throws<ErpIntegrationException>(() => router.Validate(route, c)).Code, Is.EqualTo("operation_schedule_not_supported"));
    }

    [Test]
    public async Task Odata_server_paging_is_followed_within_requested_batch()
    {
        var calls = 0;
        using var handler = new FakeHandler(_ =>
        {
            calls++;
            return calls == 1
                ? Reply("{\"value\":[{\"Id\":1,\"GoodsCode\":\"a\",\"GoodsName\":\"A\",\"NoSaleFlag\":0,\"NoStockFlag\":0}],\"@odata.nextLink\":\"?page=2\"}")
                : Reply("{\"value\":[{\"Id\":2,\"GoodsCode\":\"b\",\"GoodsName\":\"B\",\"NoSaleFlag\":0,\"NoStockFlag\":0}]}");
        });
        var result = await Provider(handler).GetProductsAsync(Connection(), 0, 2, CancellationToken.None);
        Assert.That(result.Items, Has.Count.EqualTo(2));
        Assert.That(calls, Is.EqualTo(2));
    }

    private static VaranegarProvider Provider(FakeHandler handler, ErpSecrets secrets = null, IMemoryCache memory = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
        { ["BonakMarket:ERP:AllowedAuthorities:0"] = "erp.example.test" }).Build();
        var endpoints = new ErpEndpointPolicy(config);
        var transport = new ErpHttpTransport(new FakeFactory(handler), endpoints);
        var auth = new ErpAuthenticationResolver(new IErpAuthenticator[]
        {
            new NoAuthentication(), new VaranegarAuthenticator(transport, endpoints,
                secrets ?? new ErpSecrets(new EphemeralDataProtectionProvider()), new ErpTokenCache(memory ?? new MemoryCache(new MemoryCacheOptions())))
        });
        return new VaranegarProvider(new VaranegarODataClient(new ErpHttpClient(transport, endpoints, auth), endpoints), endpoints);
    }
    private static HttpResponseMessage Reply(string json) => new(HttpStatusCode.OK)
        { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private sealed class FakeFactory(FakeHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(reply(request));
        }
    }
}
