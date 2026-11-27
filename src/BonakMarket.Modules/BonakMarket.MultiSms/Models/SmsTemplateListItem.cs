using BonakMarket.MultiSms.Services;

namespace BonakMarket.MultiSms.Models;

public sealed record SmsTemplateListItem(SmsTemplateDefinition Template, SmsReadinessResult Readiness,
    SmsLastTest LastTest = null);
public sealed record SmsLastTest(string Status, string Provider, DateTime CreatedOnUtc, int MessageId);
