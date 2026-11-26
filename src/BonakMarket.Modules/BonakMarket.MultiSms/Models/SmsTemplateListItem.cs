using BonakMarket.MultiSms.Services;

namespace BonakMarket.MultiSms.Models;

public sealed record SmsTemplateListItem(SmsTemplateDefinition Template, SmsReadinessResult Readiness);
