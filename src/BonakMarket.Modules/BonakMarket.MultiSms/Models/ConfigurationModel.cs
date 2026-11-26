namespace BonakMarket.MultiSms.Models;

[LocalizedDisplay("Plugins.Sms.MultiSms.Fields.")]
public sealed class ConfigurationModel : ModelBase
{
    [LocalizedDisplay("*Enabled")]
    public bool Enabled { get; set; }
    public bool RoutingConfigured { get; set; }
    public string GeneralPrimary { get; set; }
    public string GeneralFallback1 { get; set; }
    public string GeneralFallback2 { get; set; }
    public bool GeneralFailover { get; set; }
    public string OtpPrimary { get; set; }
    public string OtpFallback1 { get; set; }
    public string OtpFallback2 { get; set; }
    public bool OtpFailover { get; set; }
    public bool KavenegarGeneralEnabled { get; set; }
    public bool KavenegarOtpEnabled { get; set; }
    public string KavenegarOtpTemplate { get; set; }
    public bool MelipayamakGeneralEnabled { get; set; }
    public bool MelipayamakOtpEnabled { get; set; }
    public int? MelipayamakBodyId { get; set; }
    public string MelipayamakAuthMode { get; set; } = "Console";
    public bool SmsIrGeneralEnabled { get; set; }
    public bool SmsIrOtpEnabled { get; set; }
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

    [LocalizedDisplay("*SmsIrTemplateId")]
    public int? SmsIrTemplateId { get; set; }

    [LocalizedDisplay("*SmsIrCodeParameter")]
    public string SmsIrCodeParameter { get; set; } = "Code";

    [LocalizedDisplay("*NewSmsIrApiKey")]
    public string NewSmsIrApiKey { get; set; }

    [LocalizedDisplay("*ClearSmsIrApiKey")]
    public bool ClearSmsIrApiKey { get; set; }

    public bool HasSmsIrApiKey { get; set; }

    [LocalizedDisplay("*TestPhone")]
    public string TestPhone { get; set; }

    [LocalizedDisplay("*TestMessage")]
    public string TestMessage { get; set; }

    public string[] OtpReadyProviders { get; set; } = [];
    public string[] GeneralReadyProviders { get; set; } = [];

    public string[] AvailableProviders { get; set; } = [];

    public bool? TestSucceeded { get; set; }
    public string TestResult { get; set; }
}
