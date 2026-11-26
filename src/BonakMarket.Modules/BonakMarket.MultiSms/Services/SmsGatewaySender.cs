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

    // Optional history parameter keeps existing isolated sender unit tests compatible.
    public SmsGatewaySender(
        ISettingFactory settings,
        IEnumerable<ISmsProvider> providers,
        ILogger<SmsGatewaySender> logger,
        ISmsHistoryWriter history = null)
    {
        _settings = settings;
        _providers = providers.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
        _history = history;
    }

    public Task<SmsSendResult> SendAsync(SmsSendRequest request, CancellationToken cancelToken = default)
        => SendCoreAsync(request, null, cancelToken);

    // Administration-only caller; test exactly one provider using saved configuration.
    public Task<SmsSendResult> TestProviderAsync(SmsSendRequest request, string providerName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerName)) throw new ArgumentException("Choose a provider.");
        return SendCoreAsync(request, providerName, ct);
    }

    private async Task<SmsSendResult> SendCoreAsync(SmsSendRequest request, string testProvider, CancellationToken cancelToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || request.PhoneNumber.Length > 20 ||
            !request.PhoneNumber.Select((ch, i) => (ch >= '0' && ch <= '9') || (i == 0 && ch == '+')).All(valid => valid) ||
            string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 1500)
        {
            throw new ArgumentException("Invalid SMS destination or message.");
        }

        var settings = await _settings.LoadSettingsAsync<SmsGatewaySettings>();
        if (!settings.Enabled)
        {
            throw new InvalidOperationException("SMS delivery is disabled.");
        }

        bool otp = request.Kind == SmsMessageKind.Otp;
        if (otp && (request.VerificationCode is not { Length: 6 } || !request.VerificationCode.All(char.IsAsciiDigit)))
            throw new ArgumentException("Invalid verification code.");

        var names = testProvider is null ? SmsRouting.Providers(settings, otp) : new[] { testProvider };
        if (!settings.RoutingConfigured || names.Length == 0)
            throw new InvalidOperationException("Configure the SMS route first.");

        if (names.Any(x => string.Equals(x, "Mock", StringComparison.OrdinalIgnoreCase)) &&
            (names.Length != 1 || !_providers.TryGetValue(names[0], out var mock) || !SmsRouting.IsReady(mock, settings, otp)))
            throw new InvalidOperationException("Mock SMS transport must be enabled in Development and isolated.");

        SmsMessage historyMessage = null;
        if (_history is not null)
        {
            try
            {
                historyMessage = await _history.BeginAsync(request, settings, cancelToken);
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
                throw new InvalidOperationException("Unrecognized SMS provider.");
            try
            {
                var id = await AttemptAsync(provider, request, settings, historyMessage, cancelToken);
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
        SmsMessage message, CancellationToken ct)
    {
        var startedOnUtc = DateTime.UtcNow;
        var timer = Stopwatch.StartNew();
        string messageId;

        // Only provider-side failures may qualify for failover. The optional
        // database audit is always performed *outside* the provider try/catch.
        try
        {
            if (!SmsRouting.IsReady(provider, settings, request.Kind == SmsMessageKind.Otp))
            {
                throw new SmsProviderException("SMS provider is unavailable or not configured.", safeToFailover: true,
                    reason: SmsFailureReason.ProviderUnavailable);
            }

            messageId = request.Kind == SmsMessageKind.Otp
                ? await ((IOtpSmsProvider)provider).SendOtpAsync(request.PhoneNumber, request.Text, request.VerificationCode, settings, ct)
                : await provider.SendAsync(request.PhoneNumber, request.Text, settings, ct);
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
            await RecordSafelyAsync(message, provider.Name, SmsHistoryFormatter.FailedStatus(ex),
                null, diagnostic, startedOnUtc,
                (int)Math.Min(timer.ElapsedMilliseconds, int.MaxValue));
            throw;
        }

        timer.Stop();
        await RecordSafelyAsync(message, provider.Name, "Accepted", messageId,
            null, startedOnUtc, (int)Math.Min(timer.ElapsedMilliseconds, int.MaxValue));
        return messageId;
    }

    private async Task RecordSafelyAsync(SmsMessage message, string provider,
        string status, string providerMessageId, string errorCategory,
        DateTime startedOnUtc, int durationMs)
    {
        if (_history is null || message is null) return;

        try
        {
            await _history.RecordAttemptAsync(message, provider, status, providerMessageId,
                errorCategory, startedOnUtc, durationMs, CancellationToken.None);
        }
        catch (Exception)
        {
            // Never turn an accepted SMS into a transport exception or duplicate send.
            _logger.LogWarning("SMS attempt audit could not be recorded.");
        }
    }
}
