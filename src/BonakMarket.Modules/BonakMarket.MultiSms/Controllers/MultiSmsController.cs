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
        ILogger<MultiSmsController> logger = null)
    {
        _protection = protection;
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
            RoutingConfigured = true,
            Enabled = model.Enabled,
            MockEnabled = _environment.IsDevelopment() && model.MockEnabled,
            HistoryEnabled = model.HistoryEnabled,
            HistoryRetentionDays = model.HistoryRetentionDays,
            ShowOtpCodeInHistory = model.ShowOtpCodeInHistory,
            StoreNonOtpMessageBody = model.StoreNonOtpMessageBody,
            GeneralPrimary = model.GeneralPrimary?.Trim(),
            GeneralFallback1 = model.GeneralFallback1?.Trim(),
            GeneralFallback2 = model.GeneralFallback2?.Trim(),
            GeneralFailover = model.GeneralFailover,
            OtpPrimary = model.OtpPrimary?.Trim(),
            OtpFallback1 = model.OtpFallback1?.Trim(),
            OtpFallback2 = model.OtpFallback2?.Trim(),
            OtpFailover = model.OtpFailover,
            KavenegarGeneralEnabled = model.KavenegarGeneralEnabled,
            KavenegarOtpEnabled = model.KavenegarOtpEnabled,
            KavenegarOtpTemplate = model.KavenegarOtpTemplate?.Trim(),
            MelipayamakGeneralEnabled = model.MelipayamakGeneralEnabled,
            MelipayamakOtpEnabled = model.MelipayamakOtpEnabled,
            MelipayamakBodyId = model.MelipayamakBodyId,
            MelipayamakAuthMode = model.MelipayamakAuthMode?.Trim(),
            SmsIrGeneralEnabled = model.SmsIrGeneralEnabled,
            SmsIrOtpEnabled = model.SmsIrOtpEnabled,
            KavenegarEnabled = model.KavenegarEnabled,
            KavenegarSender = model.KavenegarSender?.Trim(),
            SmsIrEnabled = model.SmsIrEnabled,
            SmsIrLineNumber = model.SmsIrLineNumber,
            SmsIrTemplateId = model.SmsIrTemplateId,
            SmsIrCodeParameter = model.SmsIrCodeParameter?.Trim(),
            MelipayamakEnabled = model.MelipayamakEnabled,
            MelipayamakSender = model.MelipayamakSender?.Trim(),
            MelipayamakUsername = model.MelipayamakUsername?.Trim(),
        };

        // Old routing settings are deliberately not migrated. Require fresh secrets on first save.
        candidate.EncryptedKavenegarApiKey = ReadSecret(model.NewKavenegarApiKey, model.ClearKavenegarApiKey, settings.RoutingConfigured ? settings.EncryptedKavenegarApiKey : null);
        candidate.EncryptedMelipayamakPassword = ReadSecret(model.NewMelipayamakPassword, model.ClearMelipayamakPassword, settings.RoutingConfigured ? settings.EncryptedMelipayamakPassword : null);
        candidate.EncryptedSmsIrApiKey = ReadSecret(model.NewSmsIrApiKey, model.ClearSmsIrApiKey, settings.RoutingConfigured ? settings.EncryptedSmsIrApiKey : null);
        candidate.EncryptedMelipayamakApiToken = ReadSecret(model.NewMelipayamakApiToken, model.ClearMelipayamakApiToken, settings.RoutingConfigured ? settings.EncryptedMelipayamakApiToken : null);

        if (candidate.HistoryRetentionDays is < 1 or > 3650)
            ModelState.AddModelError(nameof(model.HistoryRetentionDays), "Retention must be between 1 and 3650 days.");
        if (candidate.MelipayamakAuthMode is not ("Console" or "Legacy"))
            ModelState.AddModelError(nameof(model.MelipayamakAuthMode), "Select a supported authentication mode.");
        if (candidate.SmsIrLineNumber is <= 0 || candidate.SmsIrTemplateId is <= 0 || candidate.MelipayamakBodyId is <= 0)
            ModelState.AddModelError("", "Line numbers and template IDs must be positive.");
        if (candidate.SmsIrOtpEnabled && !SmsIrSmsProvider.IsValidParameterName(candidate.SmsIrCodeParameter))
            ModelState.AddModelError(nameof(model.SmsIrCodeParameter), "Enter the exact SMS.ir template parameter name.");

        ValidateRoute(candidate, false);
        ValidateRoute(candidate, true);
        if (candidate.Enabled && string.IsNullOrWhiteSpace(candidate.GeneralPrimary) && string.IsNullOrWhiteSpace(candidate.OtpPrimary))
            ModelState.AddModelError("", "Configure at least one route before enabling the gateway.");

        if (!ModelState.IsValid)
        {
            model.NewKavenegarApiKey = null;
            ModelState.Remove(nameof(model.NewKavenegarApiKey));
            model.HasKavenegarApiKey = settings.RoutingConfigured && !string.IsNullOrWhiteSpace(settings.EncryptedKavenegarApiKey);
            model.NewMelipayamakPassword = null;
            ModelState.Remove(nameof(model.NewMelipayamakPassword));
            model.HasMelipayamakPassword = settings.RoutingConfigured && !string.IsNullOrWhiteSpace(settings.EncryptedMelipayamakPassword);
            model.NewSmsIrApiKey = null;
            ModelState.Remove(nameof(model.NewSmsIrApiKey));
            model.HasSmsIrApiKey = settings.RoutingConfigured && !string.IsNullOrWhiteSpace(settings.EncryptedSmsIrApiKey);
            model.NewMelipayamakApiToken = null;
            ModelState.Remove(nameof(model.NewMelipayamakApiToken));
            model.HasMelipayamakApiToken = settings.RoutingConfigured && !string.IsNullOrWhiteSpace(settings.EncryptedMelipayamakApiToken);
            model.RoutingConfigured = settings.RoutingConfigured;
            model.AvailableProviders = _providerNames;
            model.DevelopmentMode = _environment.IsDevelopment();
            return View(model);
        }

        settings.RoutingConfigured = true;
        settings.Enabled = candidate.Enabled;
        settings.MockEnabled = candidate.MockEnabled;
        settings.HistoryEnabled = candidate.HistoryEnabled;
        settings.HistoryRetentionDays = candidate.HistoryRetentionDays;
        settings.ShowOtpCodeInHistory = candidate.ShowOtpCodeInHistory;
        settings.StoreNonOtpMessageBody = candidate.StoreNonOtpMessageBody;
        settings.GeneralPrimary = candidate.GeneralPrimary;
        settings.GeneralFallback1 = candidate.GeneralFallback1;
        settings.GeneralFallback2 = candidate.GeneralFallback2;
        settings.GeneralFailover = candidate.GeneralFailover;
        settings.OtpPrimary = candidate.OtpPrimary;
        settings.OtpFallback1 = candidate.OtpFallback1;
        settings.OtpFallback2 = candidate.OtpFallback2;
        settings.OtpFailover = candidate.OtpFailover;
        settings.KavenegarGeneralEnabled = candidate.KavenegarGeneralEnabled;
        settings.KavenegarOtpEnabled = candidate.KavenegarOtpEnabled;
        settings.KavenegarOtpTemplate = candidate.KavenegarOtpTemplate;
        settings.MelipayamakGeneralEnabled = candidate.MelipayamakGeneralEnabled;
        settings.MelipayamakOtpEnabled = candidate.MelipayamakOtpEnabled;
        settings.MelipayamakBodyId = candidate.MelipayamakBodyId;
        settings.MelipayamakAuthMode = candidate.MelipayamakAuthMode;
        settings.SmsIrGeneralEnabled = candidate.SmsIrGeneralEnabled;
        settings.SmsIrOtpEnabled = candidate.SmsIrOtpEnabled;
        settings.KavenegarEnabled = candidate.KavenegarEnabled;
        settings.KavenegarSender = candidate.KavenegarSender;
        settings.SmsIrEnabled = candidate.SmsIrEnabled;
        settings.SmsIrLineNumber = candidate.SmsIrLineNumber;
        settings.SmsIrTemplateId = candidate.SmsIrTemplateId;
        settings.SmsIrCodeParameter = candidate.SmsIrCodeParameter;
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

    private void ValidateRoute(SmsGatewaySettings settings, bool otp)
    {
        var primary = otp ? settings.OtpPrimary : settings.GeneralPrimary;
        var fail = otp ? settings.OtpFailover : settings.GeneralFailover;
        var first = otp ? settings.OtpFallback1 : settings.GeneralFallback1;
        var second = otp ? settings.OtpFallback2 : settings.GeneralFallback2;
        var field = otp ? nameof(ConfigurationModel.OtpPrimary) : nameof(ConfigurationModel.GeneralPrimary);
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
            if (!IsValidProvider(name) || provider is null || !SmsRouting.IsReady(provider, settings, otp))
                ModelState.AddModelError(field, $"Provider {name} is not configured for this route.");
        }
    }

    private ConfigurationModel CreateModel(SmsGatewaySettings settings)
        => new()
        {
            AvailableProviders = _providerNames,
            DevelopmentMode = _environment.IsDevelopment(),
            RoutingConfigured = settings.RoutingConfigured,
            Enabled = settings.RoutingConfigured && settings.Enabled,
            MockEnabled = settings.RoutingConfigured ? settings.MockEnabled : default,
            HistoryEnabled = settings.HistoryEnabled,
            HistoryRetentionDays = settings.HistoryRetentionDays,
            ShowOtpCodeInHistory = settings.ShowOtpCodeInHistory,
            StoreNonOtpMessageBody = settings.StoreNonOtpMessageBody,
            GeneralPrimary = settings.RoutingConfigured ? settings.GeneralPrimary : default,
            GeneralFallback1 = settings.RoutingConfigured ? settings.GeneralFallback1 : default,
            GeneralFallback2 = settings.RoutingConfigured ? settings.GeneralFallback2 : default,
            GeneralFailover = settings.RoutingConfigured ? settings.GeneralFailover : default,
            OtpPrimary = settings.RoutingConfigured ? settings.OtpPrimary : default,
            OtpFallback1 = settings.RoutingConfigured ? settings.OtpFallback1 : default,
            OtpFallback2 = settings.RoutingConfigured ? settings.OtpFallback2 : default,
            OtpFailover = settings.RoutingConfigured ? settings.OtpFailover : default,
            KavenegarGeneralEnabled = settings.RoutingConfigured ? settings.KavenegarGeneralEnabled : default,
            KavenegarOtpEnabled = settings.RoutingConfigured ? settings.KavenegarOtpEnabled : default,
            KavenegarOtpTemplate = settings.RoutingConfigured ? settings.KavenegarOtpTemplate : default,
            MelipayamakGeneralEnabled = settings.RoutingConfigured ? settings.MelipayamakGeneralEnabled : default,
            MelipayamakOtpEnabled = settings.RoutingConfigured ? settings.MelipayamakOtpEnabled : default,
            MelipayamakBodyId = settings.RoutingConfigured ? settings.MelipayamakBodyId : default,
            MelipayamakAuthMode = settings.RoutingConfigured ? settings.MelipayamakAuthMode : "Console",
            SmsIrGeneralEnabled = settings.RoutingConfigured ? settings.SmsIrGeneralEnabled : default,
            SmsIrOtpEnabled = settings.RoutingConfigured ? settings.SmsIrOtpEnabled : default,
            KavenegarEnabled = settings.RoutingConfigured ? settings.KavenegarEnabled : default,
            KavenegarSender = settings.RoutingConfigured ? settings.KavenegarSender : default,
            SmsIrEnabled = settings.RoutingConfigured ? settings.SmsIrEnabled : default,
            SmsIrLineNumber = settings.RoutingConfigured ? settings.SmsIrLineNumber : default,
            SmsIrTemplateId = settings.RoutingConfigured ? settings.SmsIrTemplateId : default,
            SmsIrCodeParameter = settings.RoutingConfigured ? settings.SmsIrCodeParameter : "Code",
            MelipayamakEnabled = settings.RoutingConfigured ? settings.MelipayamakEnabled : default,
            MelipayamakSender = settings.RoutingConfigured ? settings.MelipayamakSender : default,
            MelipayamakUsername = settings.RoutingConfigured ? settings.MelipayamakUsername : default,
            HasKavenegarApiKey = settings.RoutingConfigured && !string.IsNullOrWhiteSpace(settings.EncryptedKavenegarApiKey),
            HasMelipayamakPassword = settings.RoutingConfigured && !string.IsNullOrWhiteSpace(settings.EncryptedMelipayamakPassword),
            HasSmsIrApiKey = settings.RoutingConfigured && !string.IsNullOrWhiteSpace(settings.EncryptedSmsIrApiKey),
            HasMelipayamakApiToken = settings.RoutingConfigured && !string.IsNullOrWhiteSpace(settings.EncryptedMelipayamakApiToken),
        };

    private bool IsValidProvider(string provider)
        => _providerNames.Contains(provider, StringComparer.OrdinalIgnoreCase);
}
