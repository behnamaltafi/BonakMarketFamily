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
            // Always permit turning OTP off even if a previously selected template has been removed.
            // While enabled, every selected template must be active and routable.
            if ((model.OtpLoginEnabled && (option is null || !option.IsReady)) ||
                (!model.OtpLoginEnabled && option is null && key != oldKey))
                ModelState.AddModelError(field, T("Plugins.Identity.Customer.Fields.OtpTemplateUnavailable").Value);
            return key;
        }
    }
}
