using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using BonakMarket.Core.Data;
using BonakMarket.MultiSms.Domain;

namespace BonakMarket.MultiSms.Services;

public sealed record SmsTemplateMapping(string ExternalTemplateId, IReadOnlyDictionary<string, string> Parameters);
public sealed record SmsTemplateDefinition(string Key, bool IsOtp, bool IsActive, string[] Parameters,
    IReadOnlyDictionary<string, SmsTemplateMapping> Providers, string Title = null, int Id = 0);
public sealed record SmsTemplatePayload(string ExternalTemplateId, IReadOnlyDictionary<string, string> Parameters);

public interface ISmsTemplateCatalog
{
    Task<SmsTemplateDefinition> FindAsync(string key, CancellationToken ct);
    Task<IReadOnlyList<SmsTemplateDefinition>> ListAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<SmsTemplateDefinition>>(Array.Empty<SmsTemplateDefinition>());
    Task<IReadOnlyList<SmsTemplateDefinition>> ListActiveOtpAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<SmsTemplateDefinition>>(Array.Empty<SmsTemplateDefinition>());
}

public sealed class SmsTemplateCatalog(SmartDbContext db) : ISmsTemplateCatalog
{
    public async Task<SmsTemplateDefinition> FindAsync(string key, CancellationToken ct)
    {
        if (!SmsTemplateRules.IsName(key, 80)) return null;
        key = key.ToUpperInvariant();
        var template = await db.Set<SmsTemplate>().AsNoTracking().SingleOrDefaultAsync(x => x.Key == key, ct);
        if (template is null) return null;
        var rows = await db.Set<SmsTemplateProvider>().AsNoTracking().Where(x => x.TemplateId == template.Id).ToListAsync(ct);
        return ToDefinition(template, rows);
    }

    public async Task<IReadOnlyList<SmsTemplateDefinition>> ListActiveOtpAsync(CancellationToken ct)
        => (await ListAsync(ct)).Where(x => x.IsOtp && x.IsActive).ToArray();

    public async Task<IReadOnlyList<SmsTemplateDefinition>> ListAsync(CancellationToken ct)
    {
        var definitions = await db.Set<SmsTemplate>().AsNoTracking()
            .OrderBy(x => x.Key).ToListAsync(ct);
        if (definitions.Count == 0) return Array.Empty<SmsTemplateDefinition>();

        // A bounded number of database queries regardless of the number of templates.
        var rows = await db.Set<SmsTemplateProvider>().AsNoTracking()
            .ToListAsync(ct);
        var byTemplate = rows.ToLookup(x => x.TemplateId);
        return definitions.Select(t => ToDefinition(t, byTemplate[t.Id])).ToArray();
    }

    private static SmsTemplateDefinition ToDefinition(SmsTemplate template, IEnumerable<SmsTemplateProvider> rows)
    {
        return new SmsTemplateDefinition(template.Key, template.IsOtp, template.IsActive,
            JsonSerializer.Deserialize<string[]>(template.Parameters),
            rows.ToDictionary(x => x.ProviderName, x => new SmsTemplateMapping(x.ExternalTemplateId,
                JsonSerializer.Deserialize<Dictionary<string, string>>(x.ParameterMapping)), StringComparer.OrdinalIgnoreCase),
            template.Title, template.Id);
    }
}

public static class SmsTemplateRules
{
    // Stable logical key shared by current Identity OTP flows; callers can select other keys.
    public const string DefaultOtpKey = "LOGINOTP";
    public static bool IsName(string name, int max = 50) =>
        name is { Length: > 0 } && name.Length <= max && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');

    public static bool IsValidDefinition(string[] names, bool otp) =>
        names is { Length: >= 1 and <= 10 } && names.All(x => IsName(x)) &&
        names.Distinct(StringComparer.Ordinal).Count() == names.Length &&
        (!otp || names.SequenceEqual(new[] { "Code" }));

    public static bool HasExactParameters(string[] names, IReadOnlyDictionary<string, string> values) =>
        names is not null && values is not null && values.Count == names.Length &&
        names.All(n => values.TryGetValue(n, out var v) && !string.IsNullOrWhiteSpace(v) &&
            v.Length <= 500 && !v.Any(char.IsControl));

    public static bool IsCompleteMapping(string[] names, SmsTemplateMapping mapping)
        => mapping is not null && mapping.Parameters is not null && names is not null &&
           mapping.Parameters.Count == names.Length &&
           names.All(n => mapping.Parameters.TryGetValue(n, out var v) && !string.IsNullOrWhiteSpace(v)) &&
           mapping.Parameters.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() == names.Length;

    public static bool TryMap(SmsTemplateDefinition template, string provider, IReadOnlyDictionary<string, string> values,
        out SmsTemplatePayload payload)
    {
        payload = null;
        if (template is null || !template.IsActive || !HasExactParameters(template.Parameters, values) ||
            !template.Providers.TryGetValue(provider, out var mapping) || !IsCompleteMapping(template.Parameters, mapping))
            return false;
        payload = new SmsTemplatePayload(mapping.ExternalTemplateId,
            template.Parameters.ToDictionary(n => mapping.Parameters[n], n => values[n], StringComparer.Ordinal));
        return true;
    }

    public static bool IsPositiveId(string id) => int.TryParse(id, System.Globalization.NumberStyles.None,
        System.Globalization.CultureInfo.InvariantCulture, out var value) && value > 0;
    public static bool IsPositional(IEnumerable<string> slots)
    {
        var values = slots.ToArray();
        return Enumerable.Range(0, values.Length).All(i => values.Contains(i.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparer.Ordinal));
    }
}
