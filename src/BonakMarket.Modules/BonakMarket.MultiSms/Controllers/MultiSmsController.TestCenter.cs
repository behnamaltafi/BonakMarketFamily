using System.Threading;
using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Messaging;
using BonakMarket.MultiSms.Configuration;
using BonakMarket.MultiSms.Models;
using BonakMarket.MultiSms.Services;
using BonakMarket.Web.Modelling.Settings;

namespace BonakMarket.MultiSms.Controllers;

public sealed partial class MultiSmsController
{
    [HttpGet, LoadSetting(false)]
    public async Task<IActionResult> TestCenter(SmsGatewaySettings settings, CancellationToken ct,
        string mode = "Template", string templateKey = null, string provider = null)
        => View(await PopulateTestCenterAsync(new SmsTestCenterModel
        {
            Mode = mode == "FreeText" ? "FreeText" : "Template",
            TemplateKey = templateKey, Provider = IsValidProvider(provider) ? provider : null
        }, settings, ct));

    [HttpPost, ValidateAntiForgeryToken, LoadSetting(false)]
    public async Task<IActionResult> TestCenter(SmsTestCenterModel model, SmsGatewaySettings settings, CancellationToken ct)
    {
        try
        {
            var phone = SmsPhoneNormalizer.Normalize(model.Phone);
            if (phone == null || (!string.IsNullOrWhiteSpace(model.Provider) && !IsValidProvider(model.Provider)))
                throw new ArgumentException();
            SmsSendRequest request;
            if (model.Mode == "Template")
            {
                var template = await _catalog.FindAsync(model.TemplateKey, ct);
                if (template is null || !template.IsActive || !SmsTemplateRules.IsValidDefinition(template.Parameters, template.IsOtp))
                    throw new ArgumentException();
                if (template.IsOtp)
                    request = new SmsSendRequest(phone, null, SmsMessageKind.Otp)
                    {
                        DeliveryMode = SmsDeliveryMode.Template, TemplateKey = template.Key,
                        VerificationCode = System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6")
                    };
                else
                {
                    if (model.Values == null || model.Values.Length != template.Parameters.Length) throw new ArgumentException();
                    request = new SmsSendRequest(phone, null, SmsMessageKind.Test)
                    {
                        TemplateKey = template.Key, DeliveryMode = SmsDeliveryMode.Template,
                        TemplateParameters = template.Parameters.Zip(model.Values).ToDictionary(x => x.First, x => x.Second)
                    };
                }
            }
            else if (model.Mode == "FreeText" && !string.IsNullOrWhiteSpace(model.Message))
                request = new SmsSendRequest(phone, model.Message, SmsMessageKind.Test);
            else throw new ArgumentException();

            var result = string.IsNullOrWhiteSpace(model.Provider)
                ? await _sender.TestRouteAsync(request, ct) : await _sender.TestProviderAsync(request, model.Provider, ct);
            TempData["SmsTestResult"] = T("Plugins.Sms.MultiSms.TestAccepted", result.Provider, result.MessageId).Value;
            TempData["SmsTestSucceeded"] = "true";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TempData["SmsTestResult"] = T("Plugins.Sms.MultiSms.TestRejected").Value + " (" + SmsHistoryFormatter.SafeDiagnostic(ex) + ")";
            TempData["SmsTestSucceeded"] = "false";
        }
        // Post/redirect/get: refreshing must not send another paid SMS. No message or code in URL/TempData.
        return RedirectToAction(nameof(TestCenter), new { mode = model.Mode, templateKey = model.TemplateKey, provider = model.Provider });
    }

    private async Task<SmsTestCenterModel> PopulateTestCenterAsync(SmsTestCenterModel model, SmsGatewaySettings settings, CancellationToken ct)
    {
        model.Providers = _providerNames;
        model.Templates = (await _catalog.ListAsync(ct)).Where(t => t.IsActive)
            .Select(t => new SmsTemplateListItem(t, SmsReadinessService.Evaluate(settings, _providers, t))).ToArray();
        model.DirectTemplateIssues = model.Templates.ToDictionary(t => t.Template.Key,
            t => _providers.Where(p => IsValidProvider(p.Name)).ToDictionary(p => p.Name,
                p => !settings.DeliveryRoutingConfigured ? "RoutingNotConfigured" :
                    SmsReadinessService.TemplateProviderIssue(p, settings, t.Template)));
        model.FreeTextIssues = GetProviderIssues(settings, false);
        model.FreeTextReadiness = SmsReadinessService.EvaluateFreeText(settings, _providers);
        if (string.IsNullOrWhiteSpace(model.TemplateKey))
            model.TemplateKey = model.Templates.FirstOrDefault(t => t.Template.Key == SmsTemplateRules.DefaultOtpKey)?.Template.Key
                ?? model.Templates.FirstOrDefault()?.Template.Key;
        model.Result = TempData["SmsTestResult"] as string;
        if (bool.TryParse(TempData["SmsTestSucceeded"] as string, out var success)) model.Succeeded = success;
        return model;
    }
}
