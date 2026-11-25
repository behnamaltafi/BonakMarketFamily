using System.Globalization;
using BonakMarket.Core.Messaging;
using BonakMarket.MultiSms.Configuration;

namespace BonakMarket.MultiSms.Services;

/// <summary>
/// Official legacy REST protocol of Melipayamak (Payamak Panel).
/// </summary>
public sealed class MelipayamakSmsProvider : ISmsProvider
{
    private const string Endpoint = "https://rest.payamak-panel.com/api/SendSMS/SendSMS";
    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    })
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    private readonly SmsCredentialProtection _credentials;

    public MelipayamakSmsProvider(SmsCredentialProtection credentials)
    {
        _credentials = credentials;
    }

    public string Name => "Melipayamak";

    public bool IsAvailable(SmsGatewaySettings settings)
        => settings.MelipayamakEnabled &&
           !string.IsNullOrWhiteSpace(settings.MelipayamakUsername) &&
           !string.IsNullOrWhiteSpace(settings.EncryptedMelipayamakPassword) &&
           !string.IsNullOrWhiteSpace(settings.MelipayamakSender);

    public async Task<string> SendAsync(string phone, string text, SmsGatewaySettings settings, CancellationToken cancelToken)
    {
        var password = _credentials.Unprotect(settings.EncryptedMelipayamakPassword);
        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = settings.MelipayamakUsername,
            ["password"] = password,
            ["to"] = SmsPhoneNormalizer.ToProviderPhone(phone),
            ["from"] = settings.MelipayamakSender,
            ["text"] = text,
            ["isFlash"] = "false"
        });

        using var response = await Client.PostAsync(Endpoint, body, cancelToken);
        if (!response.IsSuccessStatusCode)
        {
            bool safe = response.StatusCode is HttpStatusCode.Unauthorized or
                HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests;
            throw new SmsProviderException($"Melipayamak HTTP {(int)response.StatusCode}.", safe,
                httpStatusCode: (int)response.StatusCode);
        }

        var raw = await response.Content.ReadAsStringAsync(cancelToken);
        try
        {
            using var json = JsonDocument.Parse(raw);
            var status = json.RootElement.GetProperty("RetStatus").GetInt32();
            if (status != 1)
            {
                // Value can contain arbitrary text or a message identifier. Only retain a small
                // numeric error code on explicit rejection, never the response body.
                int? errorCode = null;
                if (json.RootElement.TryGetProperty("Value", out var error) &&
                    (error.ValueKind == JsonValueKind.String || error.ValueKind == JsonValueKind.Number) &&
                    int.TryParse(error.ValueKind == JsonValueKind.String ? error.GetString() : error.GetRawText(),
                        NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var code) &&
                    code is >= -9999 and <= 9999)
                {
                    errorCode = code;
                }

                throw new SmsProviderException($"Melipayamak rejected SMS (status {status}).", safeToFailover: true,
                    httpStatusCode: (int)response.StatusCode, providerStatus: status, providerErrorCode: errorCode);
            }

            var value = json.RootElement.GetProperty("Value");
            var id = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
            if (string.IsNullOrWhiteSpace(id) || id == "0")
            {
                throw new SmsProviderException("Melipayamak returned no message confirmation.",
                    httpStatusCode: (int)response.StatusCode, reason: SmsFailureReason.MissingConfirmation);
            }

            return id;
        }
        catch (JsonException)
        {
            throw new SmsProviderException("Melipayamak response could not be validated.",
                    httpStatusCode: (int)response.StatusCode, reason: SmsFailureReason.InvalidResponse);
        }
        catch (KeyNotFoundException)
        {
            throw new SmsProviderException("Melipayamak confirmation is incomplete.",
                    httpStatusCode: (int)response.StatusCode, reason: SmsFailureReason.IncompleteConfirmation);
        }
    }
}
