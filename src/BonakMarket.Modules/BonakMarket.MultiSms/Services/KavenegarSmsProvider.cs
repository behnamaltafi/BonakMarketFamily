using BonakMarket.Core.Messaging;
using BonakMarket.MultiSms.Configuration;

namespace BonakMarket.MultiSms.Services;

/// <summary>
/// Kavenegar REST API: POST /v1/{apiKey}/sms/send.json.
/// Do not use HttpClientFactory logging for this API: its URL contains the API key.
/// </summary>
public sealed class KavenegarSmsProvider : ISmsProvider, IOtpSmsProvider
{
    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        AllowAutoRedirect = false
    })
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    private readonly SmsCredentialProtection _credentials;

    public KavenegarSmsProvider(SmsCredentialProtection credentials)
    {
        _credentials = credentials;
    }

    public string Name => "Kavenegar";

    public bool IsAvailable(SmsGatewaySettings settings)
        => settings.KavenegarEnabled && settings.KavenegarGeneralEnabled &&
           !string.IsNullOrWhiteSpace(settings.EncryptedKavenegarApiKey) &&
           !string.IsNullOrWhiteSpace(settings.KavenegarSender);

    public bool IsOtpAvailable(SmsGatewaySettings settings)
        => settings.KavenegarEnabled && settings.KavenegarOtpEnabled &&
           !string.IsNullOrWhiteSpace(settings.EncryptedKavenegarApiKey) &&
           !string.IsNullOrWhiteSpace(settings.KavenegarOtpTemplate);

    public Task<string> SendAsync(string phone, string text, SmsGatewaySettings settings, CancellationToken cancelToken)
    {
        if (!IsAvailable(settings)) throw new SmsProviderException("Kavenegar general SMS is not configured.", true);
        return SendCoreAsync("sms/send.json", new Dictionary<string, string>
        {
            ["sender"] = settings.KavenegarSender,
            ["receptor"] = SmsPhoneNormalizer.ToProviderPhone(phone),
            ["message"] = text
        }, settings, cancelToken);
    }

    public Task<string> SendOtpAsync(string phone, string text, string verificationCode, SmsGatewaySettings settings, CancellationToken cancelToken)
    {
        if (!IsOtpAvailable(settings)) throw new SmsProviderException("Kavenegar OTP is not configured.", true);
        if (verificationCode is not { Length: 6 } || !verificationCode.All(char.IsAsciiDigit))
            throw new ArgumentException("Invalid verification code.");
        return SendCoreAsync("verify/lookup.json", new Dictionary<string, string>
        {
            ["receptor"] = SmsPhoneNormalizer.ToProviderPhone(phone),
            ["token"] = verificationCode,
            ["template"] = settings.KavenegarOtpTemplate,
            ["type"] = "sms"
        }, settings, cancelToken);
    }

    private async Task<string> SendCoreAsync(string path, Dictionary<string, string> values, SmsGatewaySettings settings, CancellationToken cancelToken)
    {
        var key = _credentials.Unprotect(settings.EncryptedKavenegarApiKey);
        var url = $"https://api.kavenegar.com/v1/{Uri.EscapeDataString(key)}/{path}";
        using var body = new FormUrlEncodedContent(values);

        // Never log the outgoing URL or raw response: they can contain credentials or OTPs.
        using var response = await Client.PostAsync(url, body, cancelToken);
        return ParseConfirmation(await response.Content.ReadAsStringAsync(cancelToken), (int)response.StatusCode);
    }

    public static string ParseConfirmation(string raw, int httpStatus)
    {
        if (httpStatus is < 200 or >= 300)
        {
            bool safe = IsSafeRejection(httpStatus);
            throw new SmsProviderException($"Kavenegar HTTP {httpStatus}.", safe, httpStatusCode: httpStatus);
        }

        try
        {
            using var json = JsonDocument.Parse(raw);
            int status = json.RootElement.GetProperty("return").GetProperty("status").GetInt32();
            if (status != 200)
            {
                throw new SmsProviderException($"Kavenegar rejected SMS (status {status}).", safeToFailover: IsSafeRejection(status),
                    httpStatusCode: httpStatus, providerStatus: status);
            }

            var entries = json.RootElement.GetProperty("entries");
            var entry = entries.ValueKind == JsonValueKind.Array && entries.GetArrayLength() == 1
                ? entries[0] : entries;
            // VerifyLookup returns an object; Send returns an array.
            if (entry.ValueKind != JsonValueKind.Object)
                throw new SmsProviderException("Kavenegar returned no message confirmation.",
                    httpStatusCode: httpStatus, reason: SmsFailureReason.MissingConfirmation);
            var id = entry.GetProperty("messageid");
            var value = id.ValueKind == JsonValueKind.String ? id.GetString() : id.GetRawText();
            if (!long.TryParse(value, out var number) || number <= 0)
                throw new SmsProviderException("Kavenegar returned an invalid message ID.");
            return value;
        }
        catch (JsonException)
        {
            throw new SmsProviderException("Kavenegar response could not be validated.",
                    httpStatusCode: httpStatus, reason: SmsFailureReason.InvalidResponse);
        }
        catch (InvalidOperationException)
        {
            throw new SmsProviderException("Kavenegar returned an invalid response.", httpStatusCode: httpStatus, reason: SmsFailureReason.InvalidResponse);
        }
        catch (KeyNotFoundException)
        {
            throw new SmsProviderException("Kavenegar confirmation is incomplete.",
                    httpStatusCode: httpStatus, reason: SmsFailureReason.IncompleteConfirmation);
        }
    }
    private static bool IsSafeRejection(int status) => status is 401 or 403 or 412 or 418 or 424 or 429;
}
