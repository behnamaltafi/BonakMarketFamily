using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Messaging;
using BonakMarket.Core.Security;
using BonakMarket.Identity.Configuration;
using BonakMarket.Identity.Models;
using BonakMarket.Web.Controllers;
using BonakMarket.Web.Modelling.Settings;

namespace BonakMarket.Identity.Controllers;

[Permission("configuration.setting")]
public sealed class CustomerIdentityAdminController(ISmsDeliveryAvailability delivery) : AdminController
{
    [LoadSetting(false)]
    public async Task<IActionResult> Configure(CustomerIdentitySettings settings, CancellationToken ct)
        => View(new IdentityConfigurationModel
        {
            OtpLoginEnabled = settings.OtpLoginEnabled,
            UseSeparateOtpTemplates = new[] { settings.LoginOtpTemplateKey, settings.PhoneEnrollmentOtpTemplateKey, settings.ReauthenticationOtpTemplateKey }
                .Select(CustomerIdentitySettings.EffectiveTemplateKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1,
            LoginOtpTemplateKey = CustomerIdentitySettings.EffectiveTemplateKey(settings.LoginOtpTemplateKey),
            PhoneEnrollmentOtpTemplateKey = CustomerIdentitySettings.EffectiveTemplateKey(settings.PhoneEnrollmentOtpTemplateKey),
            ReauthenticationOtpTemplateKey = CustomerIdentitySettings.EffectiveTemplateKey(settings.ReauthenticationOtpTemplateKey),
            OtpTemplates = await delivery.GetOtpTemplatesAsync(ct),
            OtpResendIntervalSeconds = settings.EffectiveOtpResendIntervalSeconds(),
            OtpExpirationSeconds = settings.EffectiveOtpExpirationSeconds(),
            OtpMaxDailyRequests = settings.EffectiveOtpMaxDailyRequests(),
            OtpMaxVerificationAttempts = settings.EffectiveOtpMaxVerificationAttempts(),
            SmsAsPrimaryLogin = settings.SmsAsPrimaryLogin
        });

    [HttpPost, SaveSetting(false)]
    public async Task<IActionResult> Configure(IdentityConfigurationModel model, CustomerIdentitySettings settings, CancellationToken ct)
    {
        model.OtpTemplates = await delivery.GetOtpTemplatesAsync(ct);
        if (!model.UseSeparateOtpTemplates)
        {
            model.PhoneEnrollmentOtpTemplateKey = model.LoginOtpTemplateKey;
            model.ReauthenticationOtpTemplateKey = model.LoginOtpTemplateKey;
        }
        model.LoginOtpTemplateKey = ValidateTemplate(model.LoginOtpTemplateKey, settings.LoginOtpTemplateKey, nameof(model.LoginOtpTemplateKey));
        model.PhoneEnrollmentOtpTemplateKey = ValidateTemplate(model.PhoneEnrollmentOtpTemplateKey, settings.PhoneEnrollmentOtpTemplateKey, nameof(model.PhoneEnrollmentOtpTemplateKey));
        model.ReauthenticationOtpTemplateKey = ValidateTemplate(model.ReauthenticationOtpTemplateKey, settings.ReauthenticationOtpTemplateKey, nameof(model.ReauthenticationOtpTemplateKey));

        if (!ModelState.IsValid) return View(model);
        settings.LoginOtpTemplateKey = model.LoginOtpTemplateKey;
        settings.PhoneEnrollmentOtpTemplateKey = model.PhoneEnrollmentOtpTemplateKey;
        settings.ReauthenticationOtpTemplateKey = model.ReauthenticationOtpTemplateKey;
        settings.OtpResendIntervalSeconds = model.OtpResendIntervalSeconds;
        settings.OtpExpirationSeconds = model.OtpExpirationSeconds;
        settings.OtpMaxDailyRequests = model.OtpMaxDailyRequests;
        settings.OtpMaxVerificationAttempts = model.OtpMaxVerificationAttempts;
        settings.OtpLoginEnabled = model.OtpLoginEnabled;
        settings.SmsAsPrimaryLogin = model.SmsAsPrimaryLogin;
        NotifySuccess(T("Admin.Common.DataSuccessfullySaved"));
        return RedirectToAction(nameof(Configure));

        string ValidateTemplate(string submitted, string current, string field)
        {
            var oldKey = CustomerIdentitySettings.EffectiveTemplateKey(current);
            var key = string.IsNullOrWhiteSpace(submitted) ? oldKey : CustomerIdentitySettings.EffectiveTemplateKey(submitted);
            var option = model.OtpTemplates.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            // Reject invalid new selections, but let administrators repair unrelated settings
            // during outages. Enabling login requires a routable login template, not every optional flow.
            bool isLogin = field == nameof(model.LoginOtpTemplateKey);
            bool changed = key != oldKey;
            if ((changed && option is null) ||
                (model.OtpLoginEnabled && isLogin && (!settings.OtpLoginEnabled || changed) && (option is null || !option.IsReady)))
                ModelState.AddModelError(field, T("Plugins.Identity.Customer.Fields.OtpTemplateUnavailable").Value);
            return key;
        }
    }
}
