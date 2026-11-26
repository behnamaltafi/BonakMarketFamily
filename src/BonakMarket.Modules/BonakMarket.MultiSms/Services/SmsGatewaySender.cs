using System.Diagnostics;
using Microsoft.Extensions.Logging;
using BonakMarket.Core.Configuration;
using BonakMarket.Core.Messaging;
using BonakMarket.MultiSms.Configuration;
using BonakMarket.MultiSms.Domain;

namespace BonakMarket.MultiSms.Services;

/// <summary>
/// Runtime-selected transport with best-effort history for EVERY provider attempt.
/// Never sends a second SMS after ambiguous timeouts or connection failures.
/// </summary>
public sealed class SmsGatewaySender : ISmsSender
{
    private readonly ISettingFactory _settings;
    private readonly IReadOnlyDictionary<string, ISmsProvider> _providers;
    private readonly ILogger<SmsGatewaySender> _logger;
    private readonly ISmsHistoryWriter _history;
    private readonly ISmsTemplateCatalog _templates;

    // Optional history parameter keeps existing isolated sender unit tests compatible.
    public SmsGatewaySender(
        ISettingFactory settings,
        IEnumerable<ISmsProvider> providers,
        ILogger<SmsGatewaySender> logger,
        ISmsHistoryWriter history = null, ISmsTemplateCatalog templates = null)
    {
        _settings = settings;
        _providers = providers.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
        _history = history;
        _templates = templates;
    }

    public Task<SmsSendResult> SendAsync(SmsSendRequest request, CancellationToken cancelToken = default)
        => SendCoreAsync(request, null, false, cancelToken);

    // Administration-only caller; test exactly one provider using saved configuration.
    public Task<SmsSendResult> TestProviderAsync(SmsSendRequest request, string providerName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerName)) throw new ArgumentException("Choose a provider.");
        return SendCoreAsync(request, providerName, true, ct);
    }

    public Task<SmsSendResult> TestRouteAsync(SmsSendRequest request, CancellationToken ct = default)
        => SendCoreAsync(request, null, true, ct);

    private async Task<SmsSendResult> SendCoreAsync(SmsSendRequest request, string testProvider, bool isTest, CancellationToken cancelToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.EffectiveDeliveryMode) || !Enum.IsDefined(request.Kind))
            throw new ArgumentException("Invalid SMS mode or purpose.");
        bool templateMode = request.EffectiveDeliveryMode == SmsDeliveryMode.Template;
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || request.PhoneNumber.Length > 20 ||
            !request.PhoneNumber.Select((ch, i) => (ch >= '0' && ch <= '9') || (i == 0 && ch == '+')).All(valid => valid) ||
            (!templateMode && string.IsNullOrWhiteSpace(request.Text)) || request.Text?.Length > 1500)
        {
            throw new ArgumentException("Invalid SMS destination or message.");
        }

        var phone = SmsPhoneNormalizer.Normalize(request.PhoneNumber);
        if (phone is null) throw new ArgumentException("Invalid Iranian mobile number.");
        request = request with { PhoneNumber = phone };

        var settings = await _settings.LoadSettingsAsync<SmsGatewaySettings>();
        if (!settings.Enabled && testProvider is null)
        {
            throw new InvalidOperationException("SMS delivery is disabled.");
        }

        bool otp = request.Kind == SmsMessageKind.Otp;
        if (otp && (!templateMode || request.VerificationCode is not { Length: 6 } || !request.VerificationCode.All(char.IsAsciiDigit)))
            throw new ArgumentException("OTP requires an approved template and six-digit code.");
        if (otp && request.TemplateParameters is not null)
            throw new ArgumentException("OTP parameters are derived only from VerificationCode.");
        if (!otp && !string.IsNullOrEmpty(request.VerificationCode))
            throw new ArgumentException("Verification code is only valid for OTP purpose.");
        if (!templateMode && (!string.IsNullOrWhiteSpace(request.TemplateKey) || request.TemplateParameters is not null))
            throw new ArgumentException("FreeText cannot contain template parameters.");

        SmsTemplateDefinition template = null;
        IReadOnlyDictionary<string, string> values = null;
        if (templateMode)
        {
            var key = request.TemplateKey ?? (otp ? SmsTemplateRules.DefaultOtpKey : null);
            if (!SmsTemplateRules.IsName(key, 80) || _templates is null)
                throw new InvalidOperationException("Configure the template catalog.");
            template = await _templates.FindAsync(key, cancelToken);
            values = otp ? new Dictionary<string, string> { ["Code"] = request.VerificationCode } : request.TemplateParameters;
            if (template is null || !template.IsActive || template.IsOtp != otp ||
                !SmsTemplateRules.IsValidDefinition(template.Parameters, template.IsOtp) ||
                !SmsTemplateRules.HasExactParameters(template.Parameters, values))
                throw new ArgumentException("Unknown, disabled or invalid SMS template/parameters.");
            request = request with { DeliveryMode = SmsDeliveryMode.Template, TemplateKey = template.Key };
        }

        var names = testProvider is null ? SmsRouting.Providers(settings, templateMode) : new[] { testProvider };
        if (!settings.DeliveryRoutingConfigured || names.Length == 0)
            throw new InvalidOperationException("Configure the SMS route first.");

        if (names.Any(x => string.Equals(x, "Mock", StringComparison.OrdinalIgnoreCase)) &&
            (names.Length != 1 || !_providers.TryGetValue(names[0], out var mock) || !SmsRouting.IsReady(mock, settings, templateMode)))
            throw new InvalidOperationException("Mock SMS transport must be enabled in Development and isolated.");

        SmsMessage historyMessage = null;
        if (_history is not null)
        {
            try
            {
                var historyRequest = isTest
                    ? request with { Kind = SmsMessageKind.Test, VerificationCode = null, DeliveryMode = request.EffectiveDeliveryMode } : request;
                historyMessage = await _history.BeginAsync(historyRequest, settings, cancelToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A history database outage must not prevent a real SMS from being sent.
                _logger.LogWarning("Could not initialize SMS history; continuing with provider.");
            }
        }

        for (var i = 0; i < names.Length; i++)
        {
            cancelToken.ThrowIfCancellationRequested();
            if (!_providers.TryGetValue(names[i], out var provider))
            {
                if (i + 1 < names.Length) continue;
                throw new InvalidOperationException("Unrecognized SMS provider.");
            }
            try
            {
                var id = await AttemptAsync(provider, request, settings, historyMessage, template, values, cancelToken);
                return new SmsSendResult(provider.Name, id);
            }
            catch (SmsProviderException ex) when (ex.SafeToFailover && i + 1 < names.Length && !cancelToken.IsCancellationRequested)
            {
                _logger.LogWarning("SMS provider {Provider} rejected the request; trying {Next}.", provider.Name, names[i + 1]);
            }
        }

        throw new InvalidOperationException("No SMS provider accepted the request.");
    }

    private async Task<string> AttemptAsync(
        ISmsProvider provider, SmsSendRequest request, SmsGatewaySettings settings,
        SmsMessage message, SmsTemplateDefinition template, IReadOnlyDictionary<string, string> values, CancellationToken ct)
    {
        var startedOnUtc = DateTime.UtcNow;
        var timer = Stopwatch.StartNew();
        string messageId;
        var method = provider.GetMethod(template is not null, settings);

        // Only provider-side failures may qualify for failover. The optional
        // database audit is always performed *outside* the provider try/catch.
        try
        {
            var issue = template == null ? SmsReadinessService.ProviderIssue(provider, settings, false)
                : SmsReadinessService.TemplateProviderIssue(provider, settings, template);
            if (issue != null)
            {
                var reason = Enum.TryParse<SmsFailureReason>(issue, out var specific)
                    ? specific : SmsFailureReason.ProviderUnavailable;
                throw new SmsProviderException("SMS provider is unavailable or not configured.", true, reason: reason);
            }

            if (template is null)
                messageId = await provider.SendAsync(request.PhoneNumber, request.Text, settings, ct);
            else
            {
                var transport = (ITemplateSmsProvider)provider;
                SmsTemplatePayload payload;
                if (string.Equals(provider.Name, "Mock", StringComparison.OrdinalIgnoreCase))
                    payload = new SmsTemplatePayload(template.Key, values);
                else if (!SmsTemplateRules.TryMap(template, provider.Name, values, out payload) ||
                    !transport.IsValidMapping(payload.ExternalTemplateId, payload.Parameters.Keys))
                    throw new SmsProviderException("Template mapping is not configured for this provider.", true,
                        reason: SmsFailureReason.ProviderUnavailable);
                messageId = await transport.SendTemplateAsync(request.PhoneNumber, payload, settings, ct);
            }
        }
        catch (Exception ex)
        {
            // Cancellation may happen after the provider accepted the SMS.
            // Keep an Unknown audit result, but never retry it.
            timer.Stop();
            var diagnostic = SmsHistoryFormatter.SafeDiagnostic(ex);
            if (ex is not OperationCanceledException)
            {
                // Log structured, sanitized information even when history is disabled.
                _logger.LogWarning("SMS attempt failed. Provider={Provider}; Kind={Kind}; HistoryId={HistoryId}; Diagnostic={Diagnostic}",
                    provider.Name, request.Kind, message?.Id, diagnostic);
            }
            await RecordSafelyAsync(message, provider.Name, method, SmsHistoryFormatter.FailedStatus(ex),
                null, diagnostic, startedOnUtc,
                (int)Math.Min(timer.ElapsedMilliseconds, int.MaxValue));
            throw;
        }

        timer.Stop();
        await RecordSafelyAsync(message, provider.Name, method, "Accepted", messageId,
            null, startedOnUtc, (int)Math.Min(timer.ElapsedMilliseconds, int.MaxValue));
        return messageId;
    }

    private async Task RecordSafelyAsync(SmsMessage message, string provider, string method,
        string status, string providerMessageId, string errorCategory,
        DateTime startedOnUtc, int durationMs)
    {
        if (_history is null || message is null) return;

        try
        {
            await _history.RecordAttemptAsync(message, provider, method, status, providerMessageId,
                errorCategory, startedOnUtc, durationMs, CancellationToken.None);
        }
        catch (Exception)
        {
            // Never turn an accepted SMS into a transport exception or duplicate send.
            _logger.LogWarning("SMS attempt audit could not be recorded.");
        }
    }
}
