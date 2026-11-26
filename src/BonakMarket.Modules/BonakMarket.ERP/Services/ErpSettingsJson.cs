using System.Text.Json;
using System.Text.Json.Serialization;

namespace BonakMarket.ERP.Services;
public static class ErpSettingsJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };
    public static T Read<T>(string json) where T : class, new()
    {
        try { return string.IsNullOrWhiteSpace(json) ? new T() : JsonSerializer.Deserialize<T>(json, Options)
            ?? throw new ErpIntegrationException("invalid_settings"); }
        catch (JsonException) { throw new ErpIntegrationException("invalid_settings"); }
    }
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
}
