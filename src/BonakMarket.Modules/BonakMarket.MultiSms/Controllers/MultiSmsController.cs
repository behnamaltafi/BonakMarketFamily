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
public sealed partial class MultiSmsController : AdminController
{
    private readonly SmsCredentialProtection _protection;
    private readonly ISmsTemplateCatalog _catalog;
    private readonly BonakMarket.Core.Data.SmartDbContext _db;
    private readonly SmsGatewaySender _sender;
    private readonly ISmsProvider[] _providers;
    private readonly IHostEnvironment _environment;
    private readonly SmsHistoryStore _history;
    private readonly DevelopmentSmsInbox _inbox;
    private readonly ILogger<MultiSmsController> _logger;
    private readonly string[] _providerNames;

    public MultiSmsController(
        SmsCredentialProtection protection,
        SmsGatewaySender sender,
        IEnumerable<ISmsProvider> providers,
        IHostEnvironment environment,
        SmsHistoryStore history,
        DevelopmentSmsInbox inbox,
        BonakMarket.Core.Data.SmartDbContext db,
        ISmsTemplateCatalog catalog,
        ILogger<MultiSmsController> logger = null)
    {
        _protection = protection;
        _db = db;
        _catalog = catalog;
        _sender = sender;
        _environment = environment;
        _history = history;
        _inbox = inbox;
        _logger = logger;
        _providers = providers.ToArray();
        _providerNames = _providers
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
        var candidate = new SmsGatewaySettings
        {
            DeliveryRoutingConfigured = true,
            Enabled = model.Enabled,
            MockEnabled = _environment.IsDevelopment() && model.MockEnabled,
            HistoryEnabled = model.HistoryEnabled,
            HistoryRetentionDays = model.HistoryRetentionDays,
            ShowOtpCodeInHistory = model.ShowOtpCodeInHistory,
            StoreNonOtpMessageBody = model.StoreNonOtpMessageBody,
            FreeTextPrimary = model.FreeTextPrimary?.Trim(),
            FreeTextFallback1 = model.FreeTextFallback1?.Trim(),
            FreeTextFallback2 = model.FreeTextFallback2?.Trim(),
            FreeTextFailover = model.FreeTextFailover,
            TemplatePrimary = model.TemplatePrimary?.Trim(),
            TemplateFallback1 = model.TemplateFallback1?.Trim(),
            TemplateFallback2 = model.TemplateFallback2?.Trim(),
            TemplateFailover = model.TemplateFailover,
            KavenegarFreeTextEnabled = model.KavenegarFreeTextEnabled,
            KavenegarTemplateEnabled = model.KavenegarTemplateEnabled,
            MelipayamakFreeTextEnabled = model.MelipayamakFreeTextEnabled,
            MelipayamakTemplateEnabled = model.MelipayamakTemplateEnabled,
            MelipayamakAuthMode = model.MelipayamakAuthMode?.Trim(),
            SmsIrFreeTextEnabled = model.SmsIrFreeTextEnabled,
            SmsIrTemplateEnabled = model.SmsIrTemplateEnabled,
            KavenegarEnabled = model.KavenegarEnabled,
            KavenegarSender = model.KavenegarSender?.Trim(),
            SmsIrEnabled = model.SmsIrEnabled,
            SmsIrLineNumber = model.SmsIrLineNumber,
            MelipayamakEnabled = model.MelipayamakEnabled,
            MelipayamakSender = model.MelipayamakSender?.Trim(),
            MelipayamakUsername = model.MelipayamakUsername?.Trim(),
        };

        // Old routing settings are deliberately not migrated. Require fresh secrets on first save.
        candidate.EncryptedKavenegarApiKey = ReadSecret(model.NewKavenegarApiKey, model.ClearKavenegarApiKey, settings.DeliveryRoutingConfigured ? settings.EncryptedKavenegarApiKey : null);
        candidate.EncryptedMelipayamakPassword = ReadSecret(model.NewMelipayamakPassword, model.ClearMelipayamakPassword, settings.DeliveryRoutingConfigured ? settings.EncryptedMelipayamakPassword : null);
        candidate.EncryptedSmsIrApiKey = ReadSecret(model.NewSmsIrApiKey, model.ClearSmsIrApiKey, settings.DeliveryRoutingConfigured ? settings.EncryptedSmsIrApiKey : null);
        candidate.EncryptedMelipayamakApiToken = ReadSecret(model.NewMelipayamakApiToken, model.ClearMelipayamakApiToken, settings.DeliveryRoutingConfigured ? settings.EncryptedMelipayamakApiToken : null);

        if (candidate.HistoryRetentionDays is < 1 or > 3650)
            ModelState.AddModelError(nameof(model.HistoryRetentionDays), "Retention must be between 1 and 3650 days.");
        if (candidate.MelipayamakAuthMode is not ("Console" or "Legacy"))
            ModelState.AddModelError(nameof(model.MelipayamakAuthMode), "Select a supported authentication mode.");
        if (candidate.SmsIrLineNumber is <= 0)
            ModelState.AddModelError(nameof(model.SmsIrLineNumber), "Line number must be positive.");

        ValidateRoute(candidate, false);
        ValidateRoute(candidate, true);
        if (candidate.Enabled && string.IsNullOrWhiteSpace(candidate.FreeTextPrimary) && string.IsNullOrWhiteSpace(candidate.TemplatePrimary))
            ModelState.AddModelError("", "Configure at least one route before enabling the gateway.");

        if (!ModelState.IsValid)
        {
            model.NewKavenegarApiKey = null;
            ModelState.Remove(nameof(model.NewKavenegarApiKey));
            model.HasKavenegarApiKey = settings.DeliveryRoutingConfigured && !string.IsNullOrWhiteSpace(settings.EncryptedKavenegarApiKey);
            model.NewMelipayamakPassword = null;
            ModelState.Remove(nameof(model.NewMelipayamakPassword));
            model.HasMelipayamakPassword = settings.DeliveryRoutingConfigured && !string.IsNullOrWhiteSpace(settings.EncryptedMelipayamakPassword);
            model.NewSmsIrApiKey = null;
            ModelState.Remove(nameof(model.NewSmsIrApiKey));
            model.HasSmsIrApiKey = settings.DeliveryRoutingConfigured && !string.IsNullOrWhiteSpace(settings.EncryptedSmsIrApiKey);
            model.NewMelipayamakApiToken = null;
            ModelState.Remove(nameof(model.NewMelipayamakApiToken));
            model.HasMelipayamakApiToken = settings.DeliveryRoutingConfigured && !string.IsNullOrWhiteSpace(settings.EncryptedMelipayamakApiToken);
            model.DeliveryRoutingConfigured = settings.DeliveryRoutingConfigured;
            model.TemplateReadyProviders = GetReadyProviders(settings, true);
            model.TemplateProviderIssues = GetProviderIssues(settings, true);
            model.FreeTextProviderIssues = GetProviderIssues(settings, false);
            model.FreeTextReadyProviders = GetReadyProviders(settings, false);
            model.AvailableProviders = _providerNames;
            model.DevelopmentMode = _environment.IsDevelopment();
            return View(model);
        }

        settings.DeliveryRoutingConfigured = true;
        settings.Enabled = candidate.Enabled;
        settings.MockEnabled = candidate.MockEnabled;
        settings.HistoryEnabled = candidate.HistoryEnabled;
        settings.HistoryRetentionDays = candidate.HistoryRetentionDays;
        settings.ShowOtpCodeInHistory = candidate.ShowOtpCodeInHistory;
        settings.StoreNonOtpMessageBody = candidate.StoreNonOtpMessageBody;
        settings.FreeTextPrimary = candidate.FreeTextPrimary;
        settings.FreeTextFallback1 = candidate.FreeTextFallback1;
        settings.FreeTextFallback2 = candidate.FreeTextFallback2;
        settings.FreeTextFailover = candidate.FreeTextFailover;
        settings.TemplatePrimary = candidate.TemplatePrimary;
        settings.TemplateFallback1 = candidate.TemplateFallback1;
        settings.TemplateFallback2 = candidate.TemplateFallback2;
        settings.TemplateFailover = candidate.TemplateFailover;
        settings.KavenegarFreeTextEnabled = candidate.KavenegarFreeTextEnabled;
        settings.KavenegarTemplateEnabled = candidate.KavenegarTemplateEnabled;
        settings.MelipayamakFreeTextEnabled = candidate.MelipayamakFreeTextEnabled;
        settings.MelipayamakTemplateEnabled = candidate.MelipayamakTemplateEnabled;
        settings.MelipayamakAuthMode = candidate.MelipayamakAuthMode;
        settings.SmsIrFreeTextEnabled = candidate.SmsIrFreeTextEnabled;
        settings.SmsIrTemplateEnabled = candidate.SmsIrTemplateEnabled;
        settings.KavenegarEnabled = candidate.KavenegarEnabled;
        settings.KavenegarSender = candidate.KavenegarSender;
        settings.SmsIrEnabled = candidate.SmsIrEnabled;
        settings.SmsIrLineNumber = candidate.SmsIrLineNumber;
        settings.MelipayamakEnabled = candidate.MelipayamakEnabled;
        settings.MelipayamakSender = candidate.MelipayamakSender;
        settings.MelipayamakUsername = candidate.MelipayamakUsername;
        settings.EncryptedKavenegarApiKey = candidate.EncryptedKavenegarApiKey;
        settings.EncryptedMelipayamakPassword = candidate.EncryptedMelipayamakPassword;
        settings.EncryptedSmsIrApiKey = candidate.EncryptedSmsIrApiKey;
        settings.EncryptedMelipayamakApiToken = candidate.EncryptedMelipayamakApiToken;
        // Clear obsolete routing values; no implicit migration or fallback.
        settings.PrimaryProvider = null;
        settings.FallbackProvider = null;
        settings.EnableFailover = false;

        NotifySuccess(T("Admin.Common.DataSuccessfullySaved"));
        return RedirectToAction(nameof(Configure));
    }

    [HttpPost, LoadSetting(false)]
    public async Task<IActionResult> TestSms(ConfigurationModel input, SmsGatewaySettings settings, CancellationToken cancelToken)
    {
        var model = CreateModel(settings);
        model.TestPhone = input.TestPhone;
        model.TestProvider = input.TestProvider;
        model.TestOtp = input.TestOtp;
        model.TestMessage = null; // Do not echo OTP-like text or the submitted body in a response.
        ModelState.Clear();

        try
        {
            var phone = SmsPhoneNormalizer.Normalize(input.TestPhone);
            if (phone is null || !IsValidProvider(input.TestProvider))
                throw new ArgumentException("Select a provider and enter a valid mobile number.");

            SmsSendRequest request;
            if (input.TestOtp)
            {
                var code = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
                request = new SmsSendRequest(phone, $"Test verification code: {code}", SmsMessageKind.Otp) { VerificationCode = code };
            }
            else
            {
                if (string.IsNullOrWhiteSpace(input.TestMessage)) throw new ArgumentException("Enter a test message.");
                request = new SmsSendRequest(phone, input.TestMessage, SmsMessageKind.Test);
            }

            var result = await _sender.TestProviderAsync(request, input.TestProvider, cancelToken);
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

    private string ReadSecret(string value, bool clear, string existing)
        => clear ? null : string.IsNullOrWhiteSpace(value) ? existing : _protection.Protect(value.Trim());

    private void ValidateRoute(SmsGatewaySettings settings, bool templateMode)
    {
        var primary = templateMode ? settings.TemplatePrimary : settings.FreeTextPrimary;
        var fail = templateMode ? settings.TemplateFailover : settings.FreeTextFailover;
        var first = templateMode ? settings.TemplateFallback1 : settings.FreeTextFallback1;
        var second = templateMode ? settings.TemplateFallback2 : settings.FreeTextFallback2;
        var field = templateMode ? nameof(ConfigurationModel.TemplatePrimary) : nameof(ConfigurationModel.FreeTextPrimary);
        var names = new[] { primary, fail ? first : null, fail ? second : null }
            .Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        if (names.Length == 0) return;
        if (string.IsNullOrWhiteSpace(primary) || (fail && string.IsNullOrWhiteSpace(first)) ||
            names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length ||
            (names.Length > 1 && names.Any(x => string.Equals(x, "Mock", StringComparison.OrdinalIgnoreCase))))
        {
            ModelState.AddModelError(field, "Choose unique providers in order; Mock must be isolated.");
            return;
        }
        foreach (var name in names)
        {
            var provider = _providers.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (!IsValidProvider(name) || provider is null)
                ModelState.AddModelError(field, $"Unknown provider: {name}.");
            else if (settings.Enabled && SmsReadinessService.ProviderIssue(provider, settings, templateMode) is string issue)
                ModelState.AddModelError(field, name + ": " + T("Plugins.Sms.MultiSms.Readiness." + issue).Value);
        }
    }

    private ConfigurationModel CreateModel(SmsGatewaySettings settings)
        => new()
        {
            AvailableProviders = _providerNames,
            DevelopmentMode = _environment.IsDevelopment(),
            DeliveryRoutingConfigured = settings.DeliveryRoutingConfigured,
            TemplateReadyProviders = GetReadyProviders(settings, true),
            TemplateProviderIssues = GetProviderIssues(settings, true),
            FreeTextProviderIssues = GetProviderIssues(settings, false),
            FreeTextReadyProviders = GetReadyProviders(settings, false),
            Enabled = settings.DeliveryRoutingConfigured && settings.Enabled,
            MockEnabled = settings.DeliveryRoutingConfigured ? settings.MockEnabled : default,
            HistoryEnabled = settings.HistoryEnabled,
            HistoryRetentionDays = settings.HistoryRetentionDays,
            ShowOtpCodeInHistory = settings.ShowOtpCodeInHistory,
            StoreNonOtpMessageBody = settings.StoreNonOtpMessageBody,
            FreeTextPrimary = settings.DeliveryRoutingConfigured ? settings.FreeTextPrimary : default,
            FreeTextFallback1 = settings.DeliveryRoutingConfigured ? settings.FreeTextFallback1 : default,
            FreeTextFallback2 = settings.DeliveryRoutingConfigured ? settings.FreeTextFallback2 : default,
            FreeTextFailover = settings.DeliveryRoutingConfigured ? settings.FreeTextFailover : default,
            TemplatePrimary = settings.DeliveryRoutingConfigured ? settings.TemplatePrimary : default,
            TemplateFallback1 = settings.DeliveryRoutingConfigured ? settings.TemplateFallback1 : default,
            TemplateFallback2 = settings.DeliveryRoutingConfigured ? settings.TemplateFallback2 : default,
            TemplateFailover = settings.DeliveryRoutingConfigured ? settings.TemplateFailover : default,
            KavenegarFreeTextEnabled = settings.DeliveryRoutingConfigured ? settings.KavenegarFreeTextEnabled : default,
            KavenegarTemplateEnabled = settings.DeliveryRoutingConfigured ? settings.KavenegarTemplateEnabled : default,
            MelipayamakFreeTextEnabled = settings.DeliveryRoutingConfigured ? settings.MelipayamakFreeTextEnabled : default,
            MelipayamakTemplateEnabled = settings.DeliveryRoutingConfigured ? settings.MelipayamakTemplateEnabled : default,
            MelipayamakAuthMode = settings.DeliveryRoutingConfigured ? settings.MelipayamakAuthMode : "Console",
            SmsIrFreeTextEnabled = settings.DeliveryRoutingConfigured ? settings.SmsIrFreeTextEnabled : default,
            SmsIrTemplateEnabled = settings.DeliveryRoutingConfigured ? settings.SmsIrTemplateEnabled : default,
            KavenegarEnabled = settings.DeliveryRoutingConfigured ? settings.KavenegarEnabled : default,
            KavenegarSender = settings.DeliveryRoutingConfigured ? settings.KavenegarSender : default,
            SmsIrEnabled = settings.DeliveryRoutingConfigured ? settings.SmsIrEnabled : default,
            SmsIrLineNumber = settings.DeliveryRoutingConfigured ? settings.SmsIrLineNumber : default,
            MelipayamakEnabled = settings.DeliveryRoutingConfigured ? settings.MelipayamakEnabled : default,
            MelipayamakSender = settings.DeliveryRoutingConfigured ? settings.MelipayamakSender : default,
            MelipayamakUsername = settings.DeliveryRoutingConfigured ? settings.MelipayamakUsername : default,
            HasKavenegarApiKey = settings.DeliveryRoutingConfigured && !string.IsNullOrWhiteSpace(settings.EncryptedKavenegarApiKey),
            HasMelipayamakPassword = settings.DeliveryRoutingConfigured && !string.IsNullOrWhiteSpace(settings.EncryptedMelipayamakPassword),
            HasSmsIrApiKey = settings.DeliveryRoutingConfigured && !string.IsNullOrWhiteSpace(settings.EncryptedSmsIrApiKey),
            HasMelipayamakApiToken = settings.DeliveryRoutingConfigured && !string.IsNullOrWhiteSpace(settings.EncryptedMelipayamakApiToken),
        };

    private string[] GetReadyProviders(SmsGatewaySettings settings, bool templateMode)
        => !settings.DeliveryRoutingConfigured ? [] : _providers
            .Where(p => IsValidProvider(p.Name) && SmsRouting.IsReady(p, settings, templateMode))
            .Select(p => p.Name).ToArray();

    private Dictionary<string, string> GetProviderIssues(SmsGatewaySettings settings, bool templateMode)
        => _providers.Where(p => IsValidProvider(p.Name)).ToDictionary(p => p.Name,
            p => !settings.DeliveryRoutingConfigured ? "RoutingNotConfigured" :
                SmsReadinessService.ProviderIssue(p, settings, templateMode));

    private bool IsValidProvider(string provider)
        => _providerNames.Contains(provider, StringComparer.OrdinalIgnoreCase);
}
