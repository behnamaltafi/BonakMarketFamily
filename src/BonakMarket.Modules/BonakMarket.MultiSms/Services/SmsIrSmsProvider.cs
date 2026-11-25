using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BonakMarket.Core.Messaging;
using BonakMarket.MultiSms.Configuration;

namespace BonakMarket.MultiSms.Services;

/// <summary>SMS.ir v1 free-text and approved Verify template transport.</summary>
public sealed class SmsIrSmsProvider : ISmsProvider, IOtpSmsProvider
{
    private const string BaseUrl = "https://api.sms.ir/v1/send/";
    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        AllowAutoRedirect = false
    })
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    private readonly SmsCredentialProtection _credentials;
    public SmsIrSmsProvider(SmsCredentialProtection credentials) => _credentials = credentials;

    public string Name => "SmsIr";

    public static bool IsValidParameterName(string name)
        => name is { Length: >= 1 and <= 50 } && name.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_');

    public bool IsAvailable(SmsGatewaySettings settings)
        => settings.SmsIrEnabled && !string.IsNullOrWhiteSpace(settings.EncryptedSmsIrApiKey) &&
            (settings.SmsIrLineNumber is > 0 ||
             (settings.SmsIrTemplateId is > 0 && IsValidParameterName(settings.SmsIrCodeParameter)));

    public Task<string> SendAsync(string phone, string text, SmsGatewaySettings settings, CancellationToken cancelToken)
    {
        if (settings.SmsIrLineNumber is not > 0)
            throw new SmsProviderException("SMS.ir free-text line is not configured.", true, reason: SmsFailureReason.ProviderUnavailable);

        return PostAsync("bulk", new
        {
            lineNumber = settings.SmsIrLineNumber.Value,
            messageText = text,
            mobiles = new[] { GetPhone(phone) }
        }, settings, false, cancelToken);
    }

    public Task<string> SendOtpAsync(string phone, string text, string verificationCode, SmsGatewaySettings settings, CancellationToken cancelToken)
    {
        if (settings.SmsIrTemplateId is not > 0)
            return SendAsync(phone, text, settings, cancelToken);

        if (!IsValidParameterName(settings.SmsIrCodeParameter) ||
            verificationCode is not { Length: 6 } || !verificationCode.All(char.IsAsciiDigit))
            throw new SmsProviderException("SMS.ir Verify parameters are not configured.", true, reason: SmsFailureReason.ProviderUnavailable);

        return PostAsync("verify", new
        {
            mobile = GetPhone(phone),
            templateId = settings.SmsIrTemplateId.Value,
            parameters = new[] { new { name = settings.SmsIrCodeParameter, value = verificationCode } }
        }, settings, true, cancelToken);
    }

    private static string GetPhone(string phone)
    {
        var canonical = SmsPhoneNormalizer.Normalize(phone);
        if (canonical is null)
            throw new SmsProviderException("SMS.ir requires an Iranian mobile number.", true);
        return SmsPhoneNormalizer.ToProviderPhone(canonical);
    }

    private async Task<string> PostAsync<T>(string endpoint, T payload, SmsGatewaySettings settings, bool verify, CancellationToken ct)
    {
        var key = _credentials.Unprotect(settings.EncryptedSmsIrApiKey);
        using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl + endpoint)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-API-KEY", key);
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await Client.SendAsync(request, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);
        return ParseConfirmation(raw, (int)response.StatusCode, verify);
    }

    // Public for deterministic protocol tests; never return raw provider text.
    public static string ParseConfirmation(string raw, int httpStatus, bool verify)
    {
        bool httpSuccess = httpStatus is >= 200 and <= 299;
        bool explicitHttpRejection = httpStatus is 401 or 403 or 429;
        try
        {
            using var json = JsonDocument.Parse(raw);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("status", out var statusValue) ||
                statusValue.ValueKind != JsonValueKind.Number || !statusValue.TryGetInt32(out int status))
                throw new SmsProviderException("SMS.ir returned an invalid response.", explicitHttpRejection,
                    httpStatusCode: httpStatus, reason: SmsFailureReason.InvalidResponse);

            if (status != 1)
            {
                // Generic status 0 and unfamiliar errors remain ambiguous; do not resend them.
                bool safe = explicitHttpRejection || status is >= 10 and <= 16 or 20 or >= 101 and <= 110 or >= 113 and <= 119 or 123 or 124;
                // Server failures remain ambiguous even if a status is present.
                safe = safe && httpStatus < 500;
                throw new SmsProviderException("SMS.ir rejected or could not confirm the request.", safe,
                    httpStatusCode: httpStatus, providerStatus: status);
            }
            if (!httpSuccess || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                throw new SmsProviderException("SMS.ir returned no confirmation.", httpStatusCode: httpStatus,
                    reason: SmsFailureReason.MissingConfirmation);

            JsonElement id;
            if (verify)
            {
                if (!data.TryGetProperty("messageId", out id))
                    throw new SmsProviderException("SMS.ir returned no message ID.", httpStatusCode: httpStatus,
                        reason: SmsFailureReason.MissingConfirmation);
            }
            else
            {
                if (!data.TryGetProperty("messageIds", out var ids) || ids.ValueKind != JsonValueKind.Array || ids.GetArrayLength() != 1)
                    throw new SmsProviderException("SMS.ir returned no single-recipient confirmation.", httpStatusCode: httpStatus,
                        reason: SmsFailureReason.MissingConfirmation);
                id = ids[0];
                // SMS.ir explicitly reports zero for blacklist and null for an invalid recipient/message.
                if (id.ValueKind == JsonValueKind.Null ||
                    (id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out long value) && value == 0))
                    throw new SmsProviderException("SMS.ir did not accept the recipient.", true, httpStatusCode: httpStatus,
                        providerStatus: 1, providerErrorCode: id.ValueKind == JsonValueKind.Null ? null : 0);
            }

            if (id.ValueKind != JsonValueKind.Number || !id.TryGetInt64(out long messageId) || messageId <= 0)
                throw new SmsProviderException("SMS.ir returned an invalid message ID.", httpStatusCode: httpStatus,
                    reason: SmsFailureReason.IncompleteConfirmation);

            return messageId.ToString(CultureInfo.InvariantCulture);
        }
        catch (JsonException)
        {
            throw new SmsProviderException("SMS.ir response could not be validated.", explicitHttpRejection,
                httpStatusCode: httpStatus, reason: SmsFailureReason.InvalidResponse);
        }
    }
}
