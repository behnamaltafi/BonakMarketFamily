using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BonakMarket.Core.Messaging;
using BonakMarket.Core.Security;
using BonakMarket.MultiSms.Configuration;
using BonakMarket.MultiSms.Models;
using BonakMarket.MultiSms.Services;
using BonakMarket.Web.Controllers;
using BonakMarket.Web.Modelling.Settings;

namespace BonakMarket.MultiSms.Controllers;

/// <summary>
/// Protected administration-only configuration and manual delivery smoke test.
/// No anonymous send endpoint is exposed by this module.
/// </summary>
[Permission("configuration.setting")]
public sealed class MultiSmsController : AdminController
{
    private readonly SmsCredentialProtection _protection;
    private readonly ISmsSender _sender;
    private readonly IHostEnvironment _environment;
    private readonly SmsHistoryStore _history;
    private readonly DevelopmentSmsInbox _inbox;
    private readonly ILogger<MultiSmsController> _logger;
    private readonly string[] _providerNames;

    public MultiSmsController(
        SmsCredentialProtection protection,
        ISmsSender sender,
        IEnumerable<ISmsProvider> providers,
        IHostEnvironment environment,
        SmsHistoryStore history,
        DevelopmentSmsInbox inbox,
        ILogger<MultiSmsController> logger = null)
    {
        _protection = protection;
        _sender = sender;
        _environment = environment;
        _history = history;
        _inbox = inbox;
        _logger = logger;
        _providerNames = providers
            .Where(x => environment.IsDevelopment() || !string.Equals(x.Name, "Mock", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
    }

    [LoadSetting(false)]
    public IActionResult Configure(SmsGatewaySettings settings)
        => View(CreateModel(settings));

    [HttpPost, SaveSetting(false)]
    public IActionResult Configure(ConfigurationModel model, SmsGatewaySettings settings)
    {
        if (model.ClearKavenegarApiKey)
        {
            settings.EncryptedKavenegarApiKey = null;
        }
        else if (!string.IsNullOrWhiteSpace(model.NewKavenegarApiKey))
        {
            settings.EncryptedKavenegarApiKey = _protection.Protect(model.NewKavenegarApiKey);
        }

        if (model.ClearMelipayamakPassword)
        {
            settings.EncryptedMelipayamakPassword = null;
        }
        else if (!string.IsNullOrWhiteSpace(model.NewMelipayamakPassword))
        {
            settings.EncryptedMelipayamakPassword = _protection.Protect(model.NewMelipayamakPassword);
        }

        if (model.ClearSmsIrApiKey)
        {
            settings.EncryptedSmsIrApiKey = null;
        }
        else if (!string.IsNullOrWhiteSpace(model.NewSmsIrApiKey))
        {
            settings.EncryptedSmsIrApiKey = _protection.Protect(model.NewSmsIrApiKey.Trim());
        }

        if (model.SmsIrLineNumber is <= 0)
            ModelState.AddModelError(nameof(model.SmsIrLineNumber), "Enter a positive SMS.ir line number.");
        if (model.SmsIrTemplateId is <= 0)
            ModelState.AddModelError(nameof(model.SmsIrTemplateId), "Enter a positive SMS.ir template ID.");
        if (model.SmsIrTemplateId is > 0 && !SmsIrSmsProvider.IsValidParameterName(model.SmsIrCodeParameter?.Trim()))
            ModelState.AddModelError(nameof(model.SmsIrCodeParameter), "Enter the exact SMS.ir template parameter name (1-50 ASCII letters, digits or underscores).");

        if (model.Enabled && (string.Equals(model.PrimaryProvider, "SmsIr", StringComparison.OrdinalIgnoreCase) ||
            (model.EnableFailover && string.Equals(model.FallbackProvider, "SmsIr", StringComparison.OrdinalIgnoreCase))))
        {
            if (!model.SmsIrEnabled || string.IsNullOrWhiteSpace(settings.EncryptedSmsIrApiKey) ||
                (model.SmsIrLineNumber is not > 0 && model.SmsIrTemplateId is not > 0))
                ModelState.AddModelError(nameof(model.SmsIrEnabled), "Enable SMS.ir and configure its API key and line number or OTP template ID.");
        }

        if (!IsValidProvider(model.PrimaryProvider))
        {
            ModelState.AddModelError(nameof(model.PrimaryProvider), "Unsupported SMS provider.");
        }

        if (model.EnableFailover && (!IsValidProvider(model.FallbackProvider) ||
            string.Equals(model.PrimaryProvider, model.FallbackProvider, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError(nameof(model.FallbackProvider), "Choose a different supported fallback provider.");
        }

        if (model.HistoryRetentionDays < 1 || model.HistoryRetentionDays > 3650)
        {
            ModelState.AddModelError(nameof(model.HistoryRetentionDays), "Retention must be between 1 and 3650 days.");
        }

        if (model.Enabled)
        {
            if (model.PrimaryProvider == "Mock" && (!_environment.IsDevelopment() || !model.MockEnabled))
            {
                ModelState.AddModelError(nameof(model.PrimaryProvider), "Mock provider is available only when enabled in Development.");
            }
            if (model.PrimaryProvider == "Kavenegar" &&
                (!model.KavenegarEnabled || string.IsNullOrWhiteSpace(model.KavenegarSender) ||
                 string.IsNullOrWhiteSpace(settings.EncryptedKavenegarApiKey)))
            {
                ModelState.AddModelError(nameof(model.PrimaryProvider), "Configure and enable the Kavenegar sender and key.");
            }

            if (model.PrimaryProvider == "Melipayamak" &&
                (!model.MelipayamakEnabled || string.IsNullOrWhiteSpace(model.MelipayamakSender) ||
                 string.IsNullOrWhiteSpace(model.MelipayamakUsername) ||
                 string.IsNullOrWhiteSpace(settings.EncryptedMelipayamakPassword)))
            {
                ModelState.AddModelError(nameof(model.PrimaryProvider), "Configure and enable the Melipayamak credentials and sender.");
            }
        }

        if (model.Enabled && model.EnableFailover)
        {
            if (model.FallbackProvider == "Mock" && (!_environment.IsDevelopment() || !model.MockEnabled))
            {
                ModelState.AddModelError(nameof(model.FallbackProvider), "Mock fallback is available only in Development.");
            }
            if (model.FallbackProvider == "Kavenegar" &&
                (!model.KavenegarEnabled || string.IsNullOrWhiteSpace(model.KavenegarSender) ||
                 string.IsNullOrWhiteSpace(settings.EncryptedKavenegarApiKey)))
            {
                ModelState.AddModelError(nameof(model.FallbackProvider), "Configure and enable the fallback Kavenegar provider.");
            }

            if (model.FallbackProvider == "Melipayamak" &&
                (!model.MelipayamakEnabled || string.IsNullOrWhiteSpace(model.MelipayamakSender) ||
                 string.IsNullOrWhiteSpace(model.MelipayamakUsername) ||
                 string.IsNullOrWhiteSpace(settings.EncryptedMelipayamakPassword)))
            {
                ModelState.AddModelError(nameof(model.FallbackProvider), "Configure and enable the fallback Melipayamak provider.");
            }
        }

        if (!ModelState.IsValid)
        {
            model.NewSmsIrApiKey = null;
            ModelState.Remove(nameof(model.NewSmsIrApiKey));
            model.HasSmsIrApiKey = !string.IsNullOrEmpty(settings.EncryptedSmsIrApiKey);
            model.NewKavenegarApiKey = null;
            model.NewMelipayamakPassword = null;
            ModelState.Remove(nameof(model.NewKavenegarApiKey));
            ModelState.Remove(nameof(model.NewMelipayamakPassword));
            model.AvailableProviders = _providerNames;
            model.DevelopmentMode = _environment.IsDevelopment();
            model.HasKavenegarApiKey = !string.IsNullOrEmpty(settings.EncryptedKavenegarApiKey);
            model.HasMelipayamakPassword = !string.IsNullOrEmpty(settings.EncryptedMelipayamakPassword);
            return View(model);
        }

        settings.Enabled = model.Enabled;
        settings.MockEnabled = _environment.IsDevelopment() && model.MockEnabled;
        settings.HistoryEnabled = model.HistoryEnabled;
        settings.HistoryRetentionDays = model.HistoryRetentionDays;
        settings.ShowOtpCodeInHistory = model.ShowOtpCodeInHistory;
        settings.StoreNonOtpMessageBody = model.StoreNonOtpMessageBody;
        settings.PrimaryProvider = model.PrimaryProvider;
        settings.EnableFailover = model.EnableFailover;
        settings.FallbackProvider = model.FallbackProvider;
        settings.KavenegarEnabled = model.KavenegarEnabled;
        settings.KavenegarSender = model.KavenegarSender?.Trim();
        settings.SmsIrEnabled = model.SmsIrEnabled;
        settings.SmsIrLineNumber = model.SmsIrLineNumber;
        settings.SmsIrTemplateId = model.SmsIrTemplateId;
        settings.SmsIrCodeParameter = model.SmsIrCodeParameter?.Trim();
        settings.MelipayamakEnabled = model.MelipayamakEnabled;
        settings.MelipayamakSender = model.MelipayamakSender?.Trim();
        settings.MelipayamakUsername = model.MelipayamakUsername?.Trim();

        NotifySuccess(T("Admin.Common.DataSuccessfullySaved"));
        return RedirectToAction(nameof(Configure));
    }

    [HttpPost, LoadSetting(false)]
    public async Task<IActionResult> TestSms(ConfigurationModel input, SmsGatewaySettings settings, CancellationToken cancelToken)
    {
        var model = CreateModel(settings);
        model.TestPhone = input.TestPhone;
        model.TestMessage = null; // Do not echo OTP-like text or the submitted body in a response.
        ModelState.Remove(nameof(model.TestMessage));

        try
        {
            if (string.IsNullOrWhiteSpace(input.TestPhone) || string.IsNullOrWhiteSpace(input.TestMessage))
            {
                throw new ArgumentException("Enter a recipient and test message.");
            }

            var result = await _sender.SendAsync(new SmsSendRequest(input.TestPhone, input.TestMessage, SmsMessageKind.Test), cancelToken);
            model.TestSucceeded = true;
            model.TestResult = T("Plugins.Sms.MultiSms.TestAccepted", result.Provider, result.MessageId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            model.TestSucceeded = false;
            var diagnostic = SmsHistoryFormatter.SafeDiagnostic(ex);
            _logger?.LogWarning("Manual SMS test failed. Diagnostic={Diagnostic}", diagnostic);
            model.TestResult = T("Plugins.Sms.MultiSms.TestRejected").Value + " (" + diagnostic + ")";
        }

        return View(nameof(Configure), model);
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> History(
        string phone = null, string kind = null, string status = null,
        int page = 1, CancellationToken ct = default)
    {
        page = Math.Clamp(page, 1, 100_000);
        phone = string.IsNullOrWhiteSpace(phone) ? null
            : SmsPhoneNormalizer.Normalize(phone) ?? phone.Trim();
        kind = kind is "Otp" or "Test" or "General" ? kind : null;
        status = status is "Accepted" or "Rejected" or "Unknown" or "Pending" ? status : null;

        var data = await _history.SearchAsync(phone, kind, status, page, 30, ct);
        return View(new SmsHistoryModel
        {
            Messages = data.Items,
            Total = data.Total,
            Page = page,
            Phone = phone,
            Kind = kind,
            Status = status
        });
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [LoadSetting(false)]
    public async Task<IActionResult> Message(int id, SmsGatewaySettings settings, CancellationToken ct = default)
    {
        var message = await _history.FindAsync(id, ct);
        if (message is null) return NotFound();

        return View(new SmsHistoryDetailsModel
        {
            Message = message,
            OtpCode = _history.GetOtpCode(message, settings, _environment.IsDevelopment()),
            ShowOtpCodeHint = settings.HistoryEnabled && settings.ShowOtpCodeInHistory && message.Kind == "Otp",
            Attempts = await _history.AttemptsAsync(id, ct)
        });
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult DevelopmentInbox()
    {
        // Defense in depth: production NEVER reads in-memory OTPs.
        if (!_environment.IsDevelopment() || !_inbox.IsAvailable)
        {
            return NotFound();
        }

        return View(_inbox.Snapshot());
    }

    private ConfigurationModel CreateModel(SmsGatewaySettings settings)
        => new()
        {
            AvailableProviders = _providerNames,
            DevelopmentMode = _environment.IsDevelopment(),
            MockEnabled = settings.MockEnabled,
            HistoryEnabled = settings.HistoryEnabled,
            HistoryRetentionDays = settings.HistoryRetentionDays,
            ShowOtpCodeInHistory = settings.ShowOtpCodeInHistory,
            StoreNonOtpMessageBody = settings.StoreNonOtpMessageBody,
            Enabled = settings.Enabled,
            PrimaryProvider = settings.PrimaryProvider,
            EnableFailover = settings.EnableFailover,
            FallbackProvider = settings.FallbackProvider,
            KavenegarEnabled = settings.KavenegarEnabled,
            KavenegarSender = settings.KavenegarSender,
            HasKavenegarApiKey = !string.IsNullOrWhiteSpace(settings.EncryptedKavenegarApiKey),
            SmsIrEnabled = settings.SmsIrEnabled,
            SmsIrLineNumber = settings.SmsIrLineNumber,
            SmsIrTemplateId = settings.SmsIrTemplateId,
            SmsIrCodeParameter = settings.SmsIrCodeParameter,
            HasSmsIrApiKey = !string.IsNullOrWhiteSpace(settings.EncryptedSmsIrApiKey),
            MelipayamakEnabled = settings.MelipayamakEnabled,
            MelipayamakSender = settings.MelipayamakSender,
            MelipayamakUsername = settings.MelipayamakUsername,
            HasMelipayamakPassword = !string.IsNullOrWhiteSpace(settings.EncryptedMelipayamakPassword)
        };

    private bool IsValidProvider(string provider)
        => _providerNames.Contains(provider, StringComparer.OrdinalIgnoreCase);
}
