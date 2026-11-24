using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using BonakMarket.Engine.Modularity;
using Moq;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using NUnit.Framework;
using BonakMarket.Identity.Controllers;
using BonakMarket.Identity.Models;
using BonakMarket.Identity.Swagger;
using BonakMarket.StorefrontApi.Controllers;
using BonakMarket.StorefrontApi.Swagger;
using Swashbuckle.AspNetCore.Swagger;

namespace BonakMarket.Core.Tests.Platform.Messaging;

[TestFixture]
public class SwaggerContractTests
{
    [TestCase("identity", "identity", "api/identity/v1/auth/session", true)]
    [TestCase("identity", "identity", "api/sms-login/session", false)]
    [TestCase("identity", "storefront", "api/storefront/v1/cart", false)]
    [TestCase("identity", null, "api/sms-gateway/test", false)]
    [TestCase("storefront", "storefront", "api/storefront/v1/cart", true)]
    [TestCase("storefront", "identity", "api/identity/v1/me/profile", false)]
    [TestCase("catalog1", "catalog1", "odata/v1/Products", true)]
    [TestCase("catalog1", "identity", "api/identity/v1/auth/session", false)]
    [TestCase("catalog1", "identity", "api/sms-login/session", false)]
    [TestCase("catalog1", "storefront", "api/storefront/v1/cart", false)]
    public void Document_inclusion_is_independent_of_registration_order(string document, string group, string path, bool expected)
    {
        var api = new ApiDescription { GroupName = group, RelativePath = path };
        bool Original(string name, ApiDescription description) =>
            description.GroupName == null || description.GroupName == name;
        Assert.That(IdentitySwaggerConfiguration.Includes(document, api,
            (name, description) => StorefrontSwaggerConfiguration.Includes(name, description, Original)), Is.EqualTo(expected));
        Assert.That(StorefrontSwaggerConfiguration.Includes(document, api,
            (name, description) => IdentitySwaggerConfiguration.Includes(name, description, Original)), Is.EqualTo(expected));
    }

    [TestCase("/docs/identity/identity/swagger.json", true, false)]
    [TestCase("/docs/storefront/storefront/swagger.json", false, true)]
    [TestCase("/docs/identity/storefront/swagger.json", false, false)]
    [TestCase("/docs/storefront/identity/swagger.json", false, false)]
    [TestCase("/docs/identity/catalog1/swagger.json", false, false)]
    [TestCase("/docs/api/identity/swagger.json", false, false)]
    [TestCase("/docs/customer/identity/swagger.json", false, false)]
    [TestCase("/docs/identity/identity/swagger.yaml", false, false)]
    public void Only_the_owning_module_serves_its_document(string path, bool identity, bool storefront)
    {
        Assert.That(IdentitySwaggerConfiguration.IsDocumentPath(new PathString(path), "/docs/identity", "identity"), Is.EqualTo(identity));
        Assert.That(StorefrontSwaggerConfiguration.IsDocumentPath(new PathString(path)), Is.EqualTo(storefront));
    }

    [TestCase(true, "Development", false, true)]
    [TestCase(true, "Production", true, true)]
    [TestCase(true, "Production", false, false)]
    [TestCase(false, "Development", true, false)]
    [TestCase(false, "Production", true, false)]
    public void Customer_portal_discovers_storefront_from_module_catalog_without_shared_swagger_options(
        bool installed, string environmentName, bool optIn, bool expected)
    {
        var catalog = new Mock<IModuleCatalog>();
        catalog.Setup(x => x.HasModule("BonakMarket.StorefrontApi")).Returns(installed);
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.EnvironmentName).Returns(environmentName);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string> { ["StorefrontApi:EnableSwagger"] = optIn.ToString() }).Build();

        Assert.That(IdentitySwaggerConfiguration.IsStorefrontDocumentationEnabled(
            catalog.Object, configuration, environment.Object), Is.EqualTo(expected));
    }

    [Test]
    public void Auth_response_preserves_the_existing_wire_contract()
    {
        using var cookie = JsonDocument.Parse(JsonSerializer.Serialize(new SmsSignInResponse("signed_in", false, "/sms-login/profile")));
        Assert.That(cookie.RootElement.GetProperty("code").GetString(), Is.EqualTo("signed_in"));
        Assert.That(cookie.RootElement.TryGetProperty("accessToken", out _), Is.False);
        Assert.That(cookie.RootElement.TryGetProperty("refreshToken", out _), Is.False);
        Assert.That(cookie.RootElement.TryGetProperty("expiresIn", out _), Is.False);
        Assert.That(cookie.RootElement.TryGetProperty("tokenType", out _), Is.False);

        using var bearer = JsonDocument.Parse(JsonSerializer.Serialize(new SmsSignInResponse("signed_in", false, "/sms-login/profile", "access", "refresh", 600, "Bearer")));
        Assert.That(bearer.RootElement.GetProperty("accessToken").GetString(), Is.EqualTo("access"));
        Assert.That(bearer.RootElement.GetProperty("expiresIn").GetInt32(), Is.EqualTo(600));
    }

    [Test]
    public void Generated_documents_separate_customer_routes_security_and_schemas()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAntiforgery(o => o.HeaderName = "X-Test-CSRF");
        services.AddMvcCore().AddApiExplorer().ConfigureApplicationPartManager(parts =>
        {
            parts.ApplicationParts.Clear();
            parts.ApplicationParts.Add(new AssemblyPart(typeof(SmsLoginApiController).Assembly));
            parts.ApplicationParts.Add(new AssemblyPart(typeof(StorefrontController).Assembly));
        });
        // Simulate inherited OData Basic security registration, which must not leak.
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("catalog1", new OpenApiInfo { Title = "Integration", Version = "v1" });
            o.AddSecurityDefinition("Basic", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "basic" });
            o.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Basic" } }] = Array.Empty<string>()
            });
        });
        services.AddIdentitySwagger();
        services.AddStorefrontSwagger();

        using var provider = services.BuildServiceProvider();
        var swagger = provider.GetRequiredService<ISwaggerProvider>();
        var identity = swagger.GetSwagger("identity");
        var storefront = swagger.GetSwagger("storefront");
        var integration = swagger.GetSwagger("catalog1");

        Assert.That(identity.Paths.Keys, Is.Not.Empty);
        Assert.That(identity.Paths.Keys.All(x => x.StartsWith("/api/identity/v1/")), Is.True);
        Assert.That(storefront.Paths.Keys, Is.Not.Empty);
        Assert.That(storefront.Paths.Keys.All(x => x.StartsWith("/api/storefront/v1/")), Is.True);
        foreach (var path in new[] { "/wishlist", "/reward-points", "/stock-subscriptions", "/orders/{orderId}/shipments",
            "/shipments/{id}", "/recurring-payments", "/returns", "/orders/{id}/returns", "/downloads",
            "/checkout/readiness", "/checkout/start", "/checkout/shipping-options", "/checkout/payment-methods",
            "/products/{productId}/reviews", "/orders/{id}/browser-actions", "/cart/gift-cards" })
        {
            var key = "/api/storefront/v1" + path;
            Assert.That(storefront.Paths.ContainsKey(key), Is.True, key);
            foreach (var operation in storefront.Paths[key].Operations.Values)
                Assert.That(operation.Security.Count, Is.EqualTo(2), key);
        }
        Assert.That(integration.Paths.Keys, Is.Empty);
        Assert.That(integration.Components.SecuritySchemes.Keys, Does.Contain("Basic"));

        foreach (var doc in new[] { identity, storefront })
        {
            Assert.That(doc.Components.SecuritySchemes.Keys, Is.EquivalentTo(new[] { "Bearer", "CustomerCookie" }));
            Assert.That(doc.SecurityRequirements, Is.Empty);
            var ids = doc.Paths.Values.SelectMany(p => p.Operations.Values).Select(o => o.OperationId).ToArray();
            Assert.That(ids.All(id => !string.IsNullOrWhiteSpace(id)), Is.True);
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
        }

        var request = identity.Paths["/api/identity/v1/auth/sms/request"].Operations[OperationType.Post];
        Assert.That(request.Security, Is.Empty);
        Assert.That(request.Parameters.Any(p => p.Name == "X-Test-CSRF" && p.Required), Is.True);
        Assert.That(request.Responses["200"].Content, Is.Not.Empty);
        var refresh = identity.Paths["/api/identity/v1/auth/refresh"].Operations[OperationType.Post];
        Assert.That(refresh.Security, Is.Empty);
        Assert.That(refresh.Parameters.Any(p => p.Name == "X-Test-CSRF"), Is.False);
        var profile = identity.Paths["/api/identity/v1/me/profile"].Operations[OperationType.Put];
        Assert.That(profile.Security.Count, Is.EqualTo(2));
        Assert.That(profile.Parameters.Any(p => p.Name == "X-Test-CSRF" && !p.Required), Is.True);
        Assert.That(profile.Responses.ContainsKey("422"), Is.True);
        var products = storefront.Paths["/api/storefront/v1/products"].Operations[OperationType.Get];
        Assert.That(products.Security.Any(s => s.Count == 0), Is.True);
        var cart = storefront.Paths["/api/storefront/v1/cart"].Operations[OperationType.Get];
        Assert.That(cart.Security.Any(s => s.Count == 0), Is.False);
    }
}
