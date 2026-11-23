using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using BonakMarket.Caching;
using BonakMarket.Core.Catalog;
using BonakMarket.Core.Catalog.Attributes;
using BonakMarket.Core.Catalog.Products;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Checkout.Rules;
using BonakMarket.Core.Checkout.Shipping;
using BonakMarket.Core.Content.Media;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Rules;
using BonakMarket.Core.Stores;
using BonakMarket.Test.Common;

namespace BonakMarket.Core.Tests.Shipping;

[TestFixture]
public class ShippingServiceTests : ServiceTestBase
{
    ShippingSettings _shippingSettings;
    IShippingService _shippingService;
    IProductAttributeMaterializer _productAttributeMaterializer;
    IStoreContext _storeContext;
    IRequestCache _requestCache;

    [OneTimeSetUp]
    public new void SetUp()
    {
        _shippingSettings = new ShippingSettings
        {
            ActiveShippingRateComputationMethodSystemNames = new List<string>
            {
                "FixedRateTestShippingRateComputationMethod"
            }
        };

        _requestCache = new NullRequestCache();

        var downloadService = new Mock<IDownloadService>();

        _productAttributeMaterializer = new ProductAttributeMaterializer(
            null,
            null,
            NullRequestCache.Instance,
            null,
            new Lazy<IDownloadService>(() => downloadService.Object),
            null);

        var storeContextMock = new Mock<IStoreContext>();
        _storeContext = storeContextMock.Object;

        DbContext.ShippingMethods.Add(new() { Name = "1" });
        DbContext.ShippingMethods.Add(new() { Name = "2" });
        DbContext.ShippingMethods.Add(new() { Name = "3" });
        DbContext.ShippingMethods.Add(new() { Name = "4" });
        DbContext.SaveChanges();

        var ruleProviderFactoryMock = new Mock<IRuleProviderFactory>();
        ruleProviderFactoryMock.Setup(x => x.GetProvider(RuleScope.Cart, null)).Returns(new Mock<ICartRuleProvider>().Object);

        _shippingService = new ShippingService(
            _productAttributeMaterializer,
            null,
            ruleProviderFactoryMock.Object,
            _shippingSettings,
            ProviderManager,
            null,
            _requestCache,
            null,
            _storeContext,
            DbContext);
    }

    [Test]
    public async Task Can_load_shippingRateComputationMethods()
    {
        var srcm = await _shippingService.GetAllShippingMethodsAsync();
        srcm.ShouldNotBeNull();
        (srcm.Count > 0).ShouldBeTrue();
    }

    [Test]
    public void Can_load_shippingRateComputationMethod_by_systemKeyword()
    {
        var srcm = _shippingService.LoadEnabledShippingProviders(systemName: "FixedRateTestShippingRateComputationMethod").FirstOrDefault();
        srcm.Value.ShouldNotBeNull();
    }

    [Test]
    public void Can_load_active_shippingRateComputationMethods()
    {
        var srcm = _shippingService.LoadEnabledShippingProviders();
        srcm.ShouldNotBeNull();
        srcm.Any().ShouldBeTrue();
    }

    [Test]
    public async Task Can_get_shoppingCartItem_totalWeight_without_attributes()
    {
        var sci = new ShoppingCartItem
        {
            RawAttributes = "",
            Quantity = 3,
            Product = new Product
            {
                Weight = 1.5M,
                Height = 2.5M,
                Length = 3.5M,
                Width = 4.5M
            }
        };

        var item = new OrganizedShoppingCartItem(sci);

        (await _shippingService.GetCartItemWeightAsync(item)).ShouldEqual(4.5M);
    }

    [Test]
    public async Task Can_get_shoppingCart_totalWeight_without_attributes()
    {
        var sci1 = new ShoppingCartItem
        {
            RawAttributes = string.Empty,
            Quantity = 3,
            Product = new Product
            {
                Weight = 1.5M,
                Height = 2.5M,
                Length = 3.5M,
                Width = 4.5M
            }
        };
        var sci2 = new ShoppingCartItem
        {
            RawAttributes = string.Empty,
            Quantity = 4,
            Product = new Product
            {
                Weight = 11.5M,
                Height = 12.5M,
                Length = 13.5M,
                Width = 14.5M
            }
        };

        var items = new List<OrganizedShoppingCartItem>
        {
            new OrganizedShoppingCartItem(sci1),
            new OrganizedShoppingCartItem(sci2)
        };

        var customer = new Customer
        {
            Id = 1,
        };

        var cart = new ShoppingCart(customer, 0, items);

        (await _shippingService.GetCartTotalWeightAsync(cart)).ShouldEqual(50.5M);
    }
}