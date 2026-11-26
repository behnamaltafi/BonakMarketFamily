using System.Globalization;
using System.Net.Http.Json;
using BonakMarket.Core.Messaging;
using BonakMarket.MultiSms.Configuration;

namespace BonakMarket.MultiSms.Services;

/// <summary>Explicit Console token or legacy REST API-key mode; never switches modes automatically.</summary>
public sealed class MelipayamakSmsProvider : ISmsProvider, IOtpSmsProvider
{
    private const string Legacy = "https://rest.payamak-panel.com/api/SendSMS/";
    private const string ConsoleApi = "https://console.melipayamak.com/api/send/";
    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        AllowAutoRedirect = false
    }) { Timeout = TimeSpan.FromSeconds(12) };
    private readonly SmsCredentialProtection _credentials;
    public MelipayamakSmsProvider(SmsCredentialProtection credentials) => _credentials = credentials;
    public string Name => "Melipayamak";

    private static bool HasCredentials(SmsGatewaySettings s) => s.MelipayamakAuthMode switch
    {
        "Console" => !string.IsNullOrWhiteSpace(s.EncryptedMelipayamakApiToken),
        "Legacy" => !string.IsNullOrWhiteSpace(s.MelipayamakUsername) &&
                    !string.IsNullOrWhiteSpace(s.EncryptedMelipayamakPassword),
        _ => false
    };

    public bool IsAvailable(SmsGatewaySettings s) => s.MelipayamakEnabled && s.MelipayamakGeneralEnabled &&
        HasCredentials(s) && !string.IsNullOrWhiteSpace(s.MelipayamakSender);
    public bool IsOtpAvailable(SmsGatewaySettings s) => s.MelipayamakEnabled && s.MelipayamakOtpEnabled &&
        HasCredentials(s) && s.MelipayamakBodyId is > 0;

    public Task<string> SendAsync(string phone, string text, SmsGatewaySettings s, CancellationToken ct)
    {
        if (!IsAvailable(s)) throw new SmsProviderException("Melipayamak general SMS is not configured.", true);
        return SendCoreAsync(phone, text, false, s, ct);
    }
    public Task<string> SendOtpAsync(string phone, string text, string verificationCode, SmsGatewaySettings s, CancellationToken ct)
    {
        if (!IsOtpAvailable(s)) throw new SmsProviderException("Melipayamak OTP is not configured.", true);
        if (verificationCode is not { Length: 6 } || !verificationCode.All(char.IsAsciiDigit))
            throw new ArgumentException("Invalid verification code.");
        return SendCoreAsync(phone, verificationCode, true, s, ct);
    }

    private async Task<string> SendCoreAsync(string phone, string value, bool otp, SmsGatewaySettings s, CancellationToken ct)
    {
        var to = SmsPhoneNormalizer.ToProviderPhone(phone);
        var console = s.MelipayamakAuthMode == "Console";
        string url;
        HttpContent content;
        if (console)
        {
            var token = _credentials.Unprotect(s.EncryptedMelipayamakApiToken);
            url = ConsoleApi + (otp ? "shared/" : "simple/") + Uri.EscapeDataString(token);
            content = otp ? JsonContent.Create(new { bodyId = s.MelipayamakBodyId.Value, to, args = new[] { value } })
                          : JsonContent.Create(new { from = s.MelipayamakSender, to, text = value });
        }
        else
        {
            url = Legacy + (otp ? "BaseServiceNumber" : "SendSMS");
            var values = new Dictionary<string, string>
            {
                ["username"] = s.MelipayamakUsername,
                ["password"] = _credentials.Unprotect(s.EncryptedMelipayamakPassword),
                ["to"] = to, ["text"] = value
            };
            if (otp) values["bodyId"] = s.MelipayamakBodyId.Value.ToString(CultureInfo.InvariantCulture);
            else { values["from"] = s.MelipayamakSender; values["isFlash"] = "false"; }
            content = new FormUrlEncodedContent(values);
        }
        // The Console URL contains credentials; never log the URL or response body.
        using (content)
        using (var response = await Client.PostAsync(url, content, ct))
            return ParseConfirmation(await response.Content.ReadAsStringAsync(ct), (int)response.StatusCode, console);
    }

    public static string ParseConfirmation(string raw, int httpStatus, bool console)
    {
        bool safeHttp = httpStatus is 401 or 403 or 429;
        if (httpStatus is < 200 or >= 300)
            throw new SmsProviderException("Melipayamak HTTP rejection or unknown response.", safeHttp, httpStatusCode: httpStatus);
        try
        {
            using var json = JsonDocument.Parse(raw);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new JsonException();
            if (console)
            {
                // Only a positive provider-assigned ID confirms acceptance. Unknown Console
                // error shapes must not trigger failover until documented and verified.
                if (root.TryGetProperty("recId", out var receipt) && TryPositiveId(receipt, out var id))
                    return id;
                throw new SmsProviderException("Melipayamak Console did not confirm acceptance.",
                    httpStatusCode: httpStatus, reason: SmsFailureReason.MissingConfirmation);
            }
            if (!root.TryGetProperty("RetStatus", out var statusElement) || !statusElement.TryGetInt32(out var status))
                throw new JsonException();
            if (!root.TryGetProperty("Value", out var value)) throw new JsonException();
            if (status == 1 && TryPositiveId(value, out var accepted) &&
                long.Parse(accepted, CultureInfo.InvariantCulture) > 18) return accepted;
            int? code = int.TryParse(value.ToString(), out var number) && number is >= -9999 and <= 9999 ? number : null;
            bool safe = code is -111 or -110 or -109 or -108 or 0 or 2 or 3 or 4 or 5 or 9 or 10 or 12 or 15;
            throw new SmsProviderException("Melipayamak did not confirm acceptance.", safe,
                httpStatusCode: httpStatus, providerStatus: status, providerErrorCode: code);
        }
        catch (JsonException)
        {
            throw new SmsProviderException("Melipayamak response could not be validated.",
                httpStatusCode: httpStatus, reason: SmsFailureReason.InvalidResponse);
        }
    }

    private static bool TryPositiveId(JsonElement value, out string id)
    {
        id = value.ValueKind is JsonValueKind.String or JsonValueKind.Number ? value.ToString() : null;
        return long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0;
    }
}
