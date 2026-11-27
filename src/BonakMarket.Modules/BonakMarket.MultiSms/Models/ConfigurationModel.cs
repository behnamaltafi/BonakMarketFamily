namespace BonakMarket.MultiSms.Models;

[LocalizedDisplay("Plugins.Sms.MultiSms.Fields.")]
public sealed class ConfigurationModel : ModelBase
{
    [LocalizedDisplay("*Enabled")]
    public bool Enabled { get; set; }
    public bool DeliveryRoutingConfigured { get; set; }
    public string FreeTextPrimary { get; set; }
    public string FreeTextFallback1 { get; set; }
    public string FreeTextFallback2 { get; set; }
    public bool FreeTextFailover { get; set; }
    public string TemplatePrimary { get; set; }
    public string TemplateFallback1 { get; set; }
    public string TemplateFallback2 { get; set; }
    public bool TemplateFailover { get; set; }
    public bool KavenegarFreeTextEnabled { get; set; }
    public bool KavenegarTemplateEnabled { get; set; }
    public bool MelipayamakFreeTextEnabled { get; set; }
    public bool MelipayamakTemplateEnabled { get; set; }
    public string MelipayamakAuthMode { get; set; } = "Console";
    public bool SmsIrFreeTextEnabled { get; set; }
    public bool SmsIrTemplateEnabled { get; set; }
    public string NewMelipayamakApiToken { get; set; }
    public bool ClearMelipayamakApiToken { get; set; }
    public bool HasMelipayamakApiToken { get; set; }
    public string TestProvider { get; set; }
    public bool TestOtp { get; set; }

    [LocalizedDisplay("*MockEnabled")]
    public bool MockEnabled { get; set; }

    [LocalizedDisplay("*HistoryEnabled")]
    public bool HistoryEnabled { get; set; }

    [LocalizedDisplay("*HistoryRetentionDays")]
    public int HistoryRetentionDays { get; set; } = 90;

    [LocalizedDisplay("*ShowOtpCodeInHistory")]
    public bool ShowOtpCodeInHistory { get; set; }

    [LocalizedDisplay("*StoreNonOtpMessageBody")]
    public bool StoreNonOtpMessageBody { get; set; }

    public bool DevelopmentMode { get; set; }

    [LocalizedDisplay("*PrimaryProvider")]
    public string PrimaryProvider { get; set; } = "Kavenegar";

    [LocalizedDisplay("*EnableFailover")]
    public bool EnableFailover { get; set; }

    [LocalizedDisplay("*FallbackProvider")]
    public string FallbackProvider { get; set; } = "Melipayamak";

    [LocalizedDisplay("*KavenegarEnabled")]
    public bool KavenegarEnabled { get; set; }

    [LocalizedDisplay("*KavenegarSender")]
    public string KavenegarSender { get; set; }

    [LocalizedDisplay("*NewKavenegarApiKey")]
    public string NewKavenegarApiKey { get; set; }

    [LocalizedDisplay("*ClearKavenegarApiKey")]
    public bool ClearKavenegarApiKey { get; set; }

    public bool HasKavenegarApiKey { get; set; }

    [LocalizedDisplay("*MelipayamakEnabled")]
    public bool MelipayamakEnabled { get; set; }

    [LocalizedDisplay("*MelipayamakSender")]
    public string MelipayamakSender { get; set; }

    [LocalizedDisplay("*MelipayamakUsername")]
    public string MelipayamakUsername { get; set; }

    [LocalizedDisplay("*NewMelipayamakPassword")]
    public string NewMelipayamakPassword { get; set; }

    [LocalizedDisplay("*ClearMelipayamakPassword")]
    public bool ClearMelipayamakPassword { get; set; }

    public bool HasMelipayamakPassword { get; set; }

    [LocalizedDisplay("*SmsIrEnabled")]
    public bool SmsIrEnabled { get; set; }

    [LocalizedDisplay("*SmsIrLineNumber")]
    public long? SmsIrLineNumber { get; set; }



    [LocalizedDisplay("*NewSmsIrApiKey")]
    public string NewSmsIrApiKey { get; set; }

    [LocalizedDisplay("*ClearSmsIrApiKey")]
    public bool ClearSmsIrApiKey { get; set; }

    public bool HasSmsIrApiKey { get; set; }

    [LocalizedDisplay("*TestPhone")]
    public string TestPhone { get; set; }

    [LocalizedDisplay("*TestMessage")]
    public string TestMessage { get; set; }

    public Dictionary<string, string> TemplateProviderIssues { get; set; } = new();
    public Dictionary<string, string> FreeTextProviderIssues { get; set; } = new();

    public string[] TemplateReadyProviders { get; set; } = [];
    public string[] FreeTextReadyProviders { get; set; } = [];

    public string[] AvailableProviders { get; set; } = [];

    public bool? TestSucceeded { get; set; }
    public string TestResult { get; set; }
}
