using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Smartstore.Core.Configuration;
using Smartstore.Core.Messaging;
using Smartstore.MultiSms.Configuration;
using Smartstore.MultiSms.Domain;

namespace Smartstore.MultiSms.Services;

/// <summary>
/// Runtime-selected transport with best-effort history for EVERY provider attempt.
/// Never sends a second SMS after ambiguous timeouts or connection failures.
/// </summary>
public sealed class SmsGatewaySender : ISmsSender
{
    private readonly ISettingFactory _settings;
    private readonly IReadOnlyDictionary<string, ISmsProvider> _providers;
    private readonly ILogger<SmsGatewaySender> _logger;
    private readonly SmsHistoryStore _history;

    // Optional history parameter keeps existing isolated sender unit tests compatible.
    public SmsGatewaySender(
        ISettingFactory settings,
        IEnumerable<ISmsProvider> providers,
        ILogger<SmsGatewaySender> logger,
        SmsHistoryStore history = null)
    {
        _settings = settings;
        _providers = providers.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
        _history = history;
    }

    public async Task<SmsSendResult> SendAsync(SmsSendRequest request, CancellationToken cancelToken = default)
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

        if (!_providers.TryGetValue(settings.PrimaryProvider ?? "", out var primary))
        {
            throw new InvalidOperationException("Primary SMS provider is not recognized.");
        }

        // A Development configuration copied to production is never allowed to
        // silently fall back from Mock to a real payable SMS transport.
        if (string.Equals(primary.Name, "Mock", StringComparison.OrdinalIgnoreCase) &&
            !primary.IsAvailable(settings))
        {
            throw new InvalidOperationException("Mock SMS transport can only run in Development.");
        }

        var historyMessage = _history is null ? null
            : await _history.BeginAsync(request, settings, cancelToken);

        try
        {
            var messageId = await AttemptAsync(primary, request, settings, historyMessage, cancelToken);
            return new SmsSendResult(primary.Name, messageId);
        }
        catch (SmsProviderException ex) when (ex.SafeToFailover && settings.EnableFailover && !cancelToken.IsCancellationRequested)
        {
            if (string.Equals(primary.Name, settings.FallbackProvider, StringComparison.OrdinalIgnoreCase) ||
                !_providers.TryGetValue(settings.FallbackProvider ?? "", out var fallback) ||
                !fallback.IsAvailable(settings))
            {
                throw;
            }

            _logger.LogWarning(
                "SMS provider {Provider} definitely rejected delivery; switching to {Fallback}.",
                primary.Name, fallback.Name);
            var fallbackId = await AttemptAsync(fallback, request, settings, historyMessage, cancelToken);
            return new SmsSendResult(fallback.Name, fallbackId);
        }
    }

    private async Task<string> AttemptAsync(
        ISmsProvider provider, SmsSendRequest request, SmsGatewaySettings settings,
        SmsMessage message, CancellationToken ct)
    {
        var startedOnUtc = DateTime.UtcNow;
        var timer = Stopwatch.StartNew();
        try
        {
            if (!provider.IsAvailable(settings))
            {
                throw new SmsProviderException("SMS provider is unavailable or not configured.", safeToFailover: true);
            }

            var messageId = await provider.SendAsync(request.PhoneNumber, request.Text, settings, ct);
            timer.Stop();
            if (_history is not null)
            {
                await _history.RecordAttemptAsync(
                    message, provider.Name, "Accepted", messageId, null,
                    startedOnUtc, (int)Math.Min(timer.ElapsedMilliseconds, int.MaxValue), CancellationToken.None);
            }
            return messageId;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            timer.Stop();
            if (_history is not null)
            {
                await _history.RecordAttemptAsync(
                    message, provider.Name, SmsHistoryFormatter.FailedStatus(ex), null,
                    SmsHistoryFormatter.SafeCategory(ex), startedOnUtc,
                    (int)Math.Min(timer.ElapsedMilliseconds, int.MaxValue), CancellationToken.None);
            }
            throw;
        }
    }
}
