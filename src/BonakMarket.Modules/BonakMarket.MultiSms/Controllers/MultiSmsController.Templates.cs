using System.Threading;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BonakMarket.Core.Messaging;
using BonakMarket.MultiSms.Configuration;
using BonakMarket.Web.Modelling.Settings;
using BonakMarket.MultiSms.Domain;
using BonakMarket.MultiSms.Models;
using BonakMarket.MultiSms.Services;

namespace BonakMarket.MultiSms.Controllers;

public sealed partial class MultiSmsController
{
    private static string[] ParseList(string value) =>
        (value ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private ISmsProvider[] TemplateProviders() => _providers
        .Where(p => p is ITemplateSmsProvider && p.Name != "Mock").OrderBy(p => p.Name).ToArray();

    [HttpGet, LoadSetting(false)]
    public async Task<IActionResult> Templates(SmsGatewaySettings settings, CancellationToken ct)
    {
        var definitions = await _catalog.ListAsync(ct);
        // Last recorded tests are evidence, not a readiness gate. History may be disabled or pruned.
        var lastIds = _db.Set<SmsMessage>().AsNoTracking()
            .Where(m => m.Kind == "Test" && m.DeliveryMode == "Template" && m.TemplateKey != null)
            .GroupBy(m => m.TemplateKey).Select(g => g.Max(m => m.Id));
        var lastTests = await _db.Set<SmsMessage>().AsNoTracking().Where(m => lastIds.Contains(m.Id))
            .Select(m => new { m.TemplateKey, m.Status, m.Provider, m.CreatedOnUtc, m.Id }).ToListAsync(ct);
        var lookup = lastTests.ToDictionary(m => m.TemplateKey,
            m => new SmsLastTest(m.Status, m.Provider, m.CreatedOnUtc, m.Id), StringComparer.OrdinalIgnoreCase);
        return View(definitions.Select(t => new SmsTemplateListItem(t,
            SmsReadinessService.Evaluate(settings, _providers, t), lookup.GetValueOrDefault(t.Key))).ToArray());
    }

    [HttpGet, LoadSetting(false)]
    public async Task<IActionResult> Template(int id, SmsGatewaySettings settings, CancellationToken ct)
    {
        if (id == 0)
            return View(new SmsTemplateEditorModel
            {
                Mappings = TemplateProviders().Select(p => new SmsTemplateMappingEditor { Provider = p.Name,
                    ConfigurationIssue = SmsReadinessService.ProviderIssue(p, settings, true) }).ToList()
            });
        var entity = await _db.Set<SmsTemplate>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return NotFound();
        var parameters = JsonSerializer.Deserialize<string[]>(entity.Parameters);
        var rows = await _db.Set<SmsTemplateProvider>().AsNoTracking().Where(x => x.TemplateId == id).ToListAsync(ct);
        var model = new SmsTemplateEditorModel
        {
            Id = id, Key = entity.Key, Title = entity.Title, IsActive = entity.IsActive,
            IsOtp = entity.IsOtp, Parameters = string.Join(", ", parameters)
        };
        var definition = await _catalog.FindAsync(entity.Key, ct);
        model.Readiness = SmsReadinessService.Evaluate(settings, _providers, definition);
        foreach (var provider in TemplateProviders())
        {
            var row = rows.SingleOrDefault(x => x.ProviderName == provider.Name);
            var map = row is null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(row.ParameterMapping);
            model.Mappings.Add(new SmsTemplateMappingEditor
            {
                Provider = provider.Name, ExternalTemplateId = row?.ExternalTemplateId,
                Slots = map is null ? null : string.Join(", ", parameters.Select(n => map.GetValueOrDefault(n))),
                ParameterSlots = map is null ? [] : parameters.Select(n => map.GetValueOrDefault(n)).ToArray(),
                ConfigurationIssue = SmsReadinessService.TemplateProviderIssue(provider, settings, definition)
            });
        }
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, LoadSetting(false)]
    public async Task<IActionResult> Template(SmsTemplateEditorModel model, SmsGatewaySettings settings, CancellationToken ct)
    {
        model.Key = model.Key?.Trim().ToUpperInvariant();
        model.Title = model.Title?.Trim();
        var names = model.IsOtp ? new[] { "Code" } : ParseList(model.Parameters);
        model.Parameters = string.Join(", ", names);
        ModelState.Remove(nameof(model.Parameters));
        var entity = model.Id == 0 ? null :
            await _db.Set<SmsTemplate>().SingleOrDefaultAsync(x => x.Id == model.Id, ct);
        if (model.Id != 0 && entity is null) return NotFound();
        if (!SmsTemplateRules.IsName(model.Key, 80) || string.IsNullOrWhiteSpace(model.Title) || model.Title.Length > 160 ||
            !SmsTemplateRules.IsValidDefinition(names, model.IsOtp))
            ModelState.AddModelError("", "Enter a valid key, title and 1–10 distinct parameters. OTP must have only Code.");
        if (entity is not null && (entity.Key != model.Key || entity.IsOtp != model.IsOtp ||
            !JsonSerializer.Deserialize<string[]>(entity.Parameters).SequenceEqual(names)))
            ModelState.AddModelError("", "Key, purpose and parameter schema are immutable. Create another template for a different contract.");
        if (entity is null && await _db.Set<SmsTemplate>().AnyAsync(x => x.Key == model.Key, ct))
            ModelState.AddModelError(nameof(model.Key), "Template key already exists.");

        var providers = TemplateProviders();
        var submitted = model.Mappings ?? [];
        if (submitted.Count != providers.Length ||
            submitted.Select(x => x.Provider).Distinct(StringComparer.Ordinal).Count() != providers.Length ||
            providers.Any(p => !submitted.Any(x => x.Provider == p.Name)))
            return BadRequest();
        var desired = new Dictionary<string, SmsTemplateMapping>(StringComparer.Ordinal);
        foreach (var item in submitted)
        {
            var externalId = item.ExternalTemplateId?.Trim();
            item.ConfigurationIssue = SmsReadinessService.ProviderIssue(providers.Single(p => p.Name == item.Provider), settings, true);
            if (string.IsNullOrWhiteSpace(externalId)) continue;

            var slots = item.ParameterSlots is { Length: > 0 }
                ? item.ParameterSlots.Select(s => s?.Trim() ?? "").ToArray() : ParseList(item.Slots);
            if (model.IsOtp)
            {
                if (item.Provider == "Kavenegar") slots = ["token"];
                else if (item.Provider == "Melipayamak") slots = ["0"];
                else if (item.Provider == "SmsIr" && (slots.Length == 0 || slots.All(string.IsNullOrWhiteSpace))) slots = ["Code"];
            }
            item.ParameterSlots = slots;
            item.Slots = string.Join(", ", slots);
            var provider = (ITemplateSmsProvider)providers.Single(p => p.Name == item.Provider);
            if (externalId.Length > 100 || slots.Length != names.Length || slots.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 50) ||
                slots.Distinct(StringComparer.OrdinalIgnoreCase).Count() != slots.Length ||
                !provider.IsValidMapping(externalId, slots) ||
                !SmsTemplateRules.IsValidDefinition(names, model.IsOtp))
            {
                ModelState.AddModelError("", $"Invalid parameter mapping for {item.Provider}.");
                continue;
            }
            desired[item.Provider] = new SmsTemplateMapping(externalId, names.Zip(slots).ToDictionary(x => x.First, x => x.Second));
        }
        if (!ModelState.IsValid) return View(model);

        if (entity is null)
        {
            entity = new SmsTemplate { Key = model.Key, IsOtp = model.IsOtp, Parameters = JsonSerializer.Serialize(names) };
            _db.Set<SmsTemplate>().Add(entity);
        }
        entity.Title = model.Title;
        entity.IsActive = model.IsActive;
        var existing = model.Id == 0 ? new List<SmsTemplateProvider>() :
            await _db.Set<SmsTemplateProvider>().Where(x => x.TemplateId == model.Id).ToListAsync(ct);
        foreach (var row in existing)
        {
            if (!desired.Remove(row.ProviderName, out var mapping))
                _db.Set<SmsTemplateProvider>().Remove(row);
            else
            {
                row.ExternalTemplateId = mapping.ExternalTemplateId;
                row.ParameterMapping = JsonSerializer.Serialize(mapping.Parameters);
            }
        }
        foreach (var (provider, mapping) in desired)
            _db.Set<SmsTemplateProvider>().Add(new SmsTemplateProvider
            {
                Template = entity, ProviderName = provider, ExternalTemplateId = mapping.ExternalTemplateId,
                ParameterMapping = JsonSerializer.Serialize(mapping.Parameters)
            });
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("", "Could not save the template. Check for duplicate keys or concurrent updates.");
            return View(model);
        }
        NotifySuccess(T("Admin.Common.DataSuccessfullySaved"));
        return RedirectToAction(nameof(Template), new { id = entity.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TestTemplate(int id, string provider, string phone, string[] values, CancellationToken ct)
    {
        var template = await _db.Set<SmsTemplate>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (template is null) return NotFound();
        try
        {
            var normalized = SmsPhoneNormalizer.Normalize(phone);
            if (normalized is null || !IsValidProvider(provider)) throw new ArgumentException();
            var names = JsonSerializer.Deserialize<string[]>(template.Parameters);
            SmsSendRequest request;
            if (template.IsOtp)
            {
                var code = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
                request = new SmsSendRequest(normalized, null, SmsMessageKind.Otp)
                    { VerificationCode = code, TemplateKey = template.Key, DeliveryMode = SmsDeliveryMode.Template };
            }
            else
            {
                if (values is null || values.Length != names.Length) throw new ArgumentException();
                request = new SmsSendRequest(normalized, null, SmsMessageKind.Test)
                    { TemplateKey = template.Key, DeliveryMode = SmsDeliveryMode.Template,
                      TemplateParameters = names.Zip(values).ToDictionary(x => x.First, x => x.Second) };
            }
            var result = await _sender.TestProviderAsync(request, provider, ct);
            NotifySuccess(T("Plugins.Sms.MultiSms.TestAccepted", result.Provider, result.MessageId));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            NotifyError(T("Plugins.Sms.MultiSms.TestRejected").Value + " (" + SmsHistoryFormatter.SafeDiagnostic(ex) + ")");
        }
        return RedirectToAction(nameof(Template), new { id });
    }
}
