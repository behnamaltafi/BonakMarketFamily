using BonakMarket.Core.Configuration;

namespace BonakMarket.MultiSms.Configuration;

/// <summary>
/// Settings are loaded afresh through BonakMarket's settings cache on each send.
/// Secrets must contain ASP.NET Core Data Protection ciphertext, not plaintext.
/// </summary>
public sealed class SmsGatewaySettings : ISettings
{
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
    public int? SmsIrTemplateId { get; set; }
    public string SmsIrCodeParameter { get; set; } = "Code";
    public string EncryptedSmsIrApiKey { get; set; }
    public bool MelipayamakEnabled { get; set; }
    public string MelipayamakSender { get; set; }
    public string MelipayamakUsername { get; set; }
    public string EncryptedMelipayamakPassword { get; set; }
}
