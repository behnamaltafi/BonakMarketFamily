using BonakMarket.Core.Configuration;

namespace BonakMarket.MultiSms.Configuration;

/// <summary>
/// Settings are loaded afresh through BonakMarket's settings cache on each send.
/// Secrets must contain ASP.NET Core Data Protection ciphertext, not plaintext.
/// </summary>
public sealed class SmsGatewaySettings : ISettings
{
    public bool Enabled { get; set; }
    /// <summary>Only effective when the process is running in Development.</summary>
    public bool MockEnabled { get; set; }
    public bool HistoryEnabled { get; set; } = true;
    public int HistoryRetentionDays { get; set; } = 90;
    /// <summary>OTP and test bodies are never persisted regardless of this flag.</summary>
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
