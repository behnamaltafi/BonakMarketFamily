using BonakMarket.MultiSms.Services;

namespace BonakMarket.MultiSms.Models;

public sealed class SmsTestCenterModel
{
    public string Mode { get; set; } = "Template";
    public string TemplateKey { get; set; }
    public string Provider { get; set; }
    public string Phone { get; set; }
    public string Message { get; set; }
    public string[] Values { get; set; } = [];
    public IReadOnlyList<SmsTemplateListItem> Templates { get; set; } = [];
    public string[] Providers { get; set; } = [];
    public bool? Succeeded { get; set; }
    public string Result { get; set; }
}
