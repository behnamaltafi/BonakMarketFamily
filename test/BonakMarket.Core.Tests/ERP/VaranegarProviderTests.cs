using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using NUnit.Framework;
using BonakMarket.ERP.Domain;
using BonakMarket.ERP.Providers.Varanegar;
using BonakMarket.ERP.Services;

namespace BonakMarket.Core.Tests.ERP;

[TestFixture]
public class VaranegarProviderTests
{
    private static ErpConnection Connection() => new()
    {
        BaseUrl = "https://erp.example.test/thirdparty/",
        ODataPrefix = "odata/",
        Enabled = true,
        UseLogin = false
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
            Assert.That(page.Items[0].WarehouseId, Is.EqualTo("0"));
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
        c.UseLogin = true;
        c.EncryptedLoginBody = secrets.Protect("{\"exampleField\":\"sample\"}");
        var received = false;
        using var handler = new FakeHandler(request =>
        {
            if (request.Method == HttpMethod.Post)
                return Reply("{\"Token\":\"test-token\",\"expiresAt\":\"2099-01-01T00:00:00Z\"}");
            received = request.Headers.Authorization?.ToString() == "Bearer test-token";
            return Reply("{\"value\":[]}");
        });
        var provider = new VaranegarProvider(new FakeFactory(handler), secrets);
        await provider.GetProductsAsync(c, 0, 10, CancellationToken.None);
        Assert.That(received, Is.True);
        Assert.That(handler.Requests, Has.Count.EqualTo(2));
        Assert.That(handler.Requests[0].AbsolutePath, Does.EndWith("/api/auth/login"));
    }

    private static VaranegarProvider Provider(FakeHandler handler) =>
        new(new FakeFactory(handler), new ErpSecrets(new EphemeralDataProtectionProvider()));
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
