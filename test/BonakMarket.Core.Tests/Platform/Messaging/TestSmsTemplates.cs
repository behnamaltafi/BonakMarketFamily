using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BonakMarket.MultiSms.Services;

namespace BonakMarket.Core.Tests.Platform.Messaging;

internal sealed class TestSmsTemplates : ISmsTemplateCatalog
{
    private readonly Dictionary<string, SmsTemplateDefinition> _items = new(StringComparer.OrdinalIgnoreCase);
    public static TestSmsTemplates Catalog()
    {
        var c = new TestSmsTemplates();
        c._items["LOGINOTP"] = new SmsTemplateDefinition("LOGINOTP", true, true, new[] { "Code" },
            new Dictionary<string, SmsTemplateMapping>(StringComparer.OrdinalIgnoreCase)
            {
                ["Kavenegar"] = new("login", new Dictionary<string, string> { ["Code"] = "token" }),
                ["Melipayamak"] = new("123", new Dictionary<string, string> { ["Code"] = "0" }),
                ["SmsIr"] = new("456", new Dictionary<string, string> { ["Code"] = "CODE" })
            });
        c._items["ORDERSHIPPED"] = new SmsTemplateDefinition("ORDERSHIPPED", false, true, new[] { "OrderNumber", "TrackingCode" },
            new Dictionary<string, SmsTemplateMapping>(StringComparer.OrdinalIgnoreCase)
            {
                ["Kavenegar"] = new("shipment", new Dictionary<string, string> { ["OrderNumber"] = "token", ["TrackingCode"] = "token2" }),
                ["Melipayamak"] = new("777", new Dictionary<string, string> { ["OrderNumber"] = "1", ["TrackingCode"] = "0" })
            });
        return c;
    }
    public Task<SmsTemplateDefinition> FindAsync(string key, CancellationToken ct) =>
        Task.FromResult(_items.TryGetValue(key, out var item) ? item : null);
}
