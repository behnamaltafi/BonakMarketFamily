using BonakMarket.Core.Configuration;

namespace BonakMarket.MultiSms.Configuration;

/// <summary>
/// Settings are loaded afresh through BonakMarket's settings cache on each send.
/// Secrets must contain ASP.NET Core Data Protection ciphertext, not plaintext.
/// </summary>
public sealed class SmsGatewaySettings : ISettings
{
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
    public string EncryptedMelipayamakApiToken { get; set; }
    /// <summary>Only effective when the process is running in Development.</summary>
    public bool MockEnabled { get; set; }
    public bool HistoryEnabled { get; set; } = true;
    public int HistoryRetentionDays { get; set; } = 90;
    /// <summary>Opt-in temporary encrypted OTP code history; disabled by default.</summary>
    public bool ShowOtpCodeInHistory { get; set; }
    /// <summary>Only ordinary message bodies are persisted by this flag.</summary>
    public bool StoreNonOtpMessageBody { get; set; }
    public string PrimaryProvider { get; set; } = "Kavenegar";
    public bool EnableFailover { get; set; }
    public string FallbackProvider { get; set; } = "Melipayamak";
    public bool KavenegarEnabled { get; set; } = true;
    public string KavenegarSender { get; set; }
    public string EncryptedKavenegarApiKey { get; set; }
    public bool SmsIrEnabled { get; set; }
    public long? SmsIrLineNumber { get; set; }
    public string EncryptedSmsIrApiKey { get; set; }
    public bool MelipayamakEnabled { get; set; }
    public string MelipayamakSender { get; set; }
    public string MelipayamakUsername { get; set; }
    public string EncryptedMelipayamakPassword { get; set; }
}
