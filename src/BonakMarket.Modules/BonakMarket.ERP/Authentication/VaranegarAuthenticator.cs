using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BonakMarket.ERP.Domain;
using BonakMarket.ERP.Infrastructure;
using BonakMarket.ERP.Services;

namespace BonakMarket.ERP.Authentication;

public sealed class VaranegarAuthenticator(ErpHttpTransport transport, ErpEndpointPolicy endpoints, ErpSecrets secrets, ErpTokenCache cache) : IErpAuthenticator
{
    public string SystemName => "VaranegarLogin";
    public string DefaultSettingsJson => ErpSettingsJson.Write(new VaranegarAuthenticationSettings());
    public void Validate(ErpConnection c)
    {
        var s = ErpSettingsJson.Read<VaranegarAuthenticationSettings>(c.AuthenticationSettingsJson);
        endpoints.Relative(endpoints.ValidateBase(c.BaseUrl), s.LoginPath);
        if (!string.IsNullOrWhiteSpace(s.AuthorizationScheme) && !Regex.IsMatch(s.AuthorizationScheme, "^[A-Za-z][A-Za-z0-9_-]{0,39}$"))
            throw new ErpIntegrationException("invalid_authorization_scheme");
        if (string.IsNullOrWhiteSpace(s.UsernameField) || string.IsNullOrWhiteSpace(s.PasswordField))
            throw new ErpIntegrationException("invalid_login_field");
        var keys = new[] { s.UsernameField, s.PasswordField, s.FiscalYearField, s.DistributionCenterField }
            .Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        if (keys.Any(x => x.Length > 80 || x.Any(char.IsControl)) || keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != keys.Length ||
            (!string.IsNullOrWhiteSpace(s.FiscalYear) != !string.IsNullOrWhiteSpace(s.FiscalYearField)) ||
            (!string.IsNullOrWhiteSpace(s.DistributionCenterId) != !string.IsNullOrWhiteSpace(s.DistributionCenterField)))
            throw new ErpIntegrationException("invalid_login_field");
        if (c.Enabled && string.IsNullOrWhiteSpace(c.EncryptedAuthenticationSettings))
            throw new ErpIntegrationException("login_not_configured");
    }

    public async Task AuthenticateAsync(ErpConnection c, HttpRequestMessage request, CancellationToken ct)
    {
        Validate(c);
        if (string.IsNullOrWhiteSpace(c.EncryptedAuthenticationSettings)) throw new ErpIntegrationException("login_not_configured");
        var token = await cache.GetAsync(c, () => LoginAsync(c, ct), ct);
        if (!request.Headers.TryAddWithoutValidation("Authorization", token.Authorization))
            throw new ErpIntegrationException("invalid_authorization");
    }
    public void Invalidate(ErpConnection c, HttpRequestMessage rejectedRequest)
    {
        if (rejectedRequest.Headers.TryGetValues("Authorization", out var values))
            cache.Invalidate(c, values.FirstOrDefault());
    }
    private async Task<ErpAccessToken> LoginAsync(ErpConnection c, CancellationToken ct)
    {
        var s = ErpSettingsJson.Read<VaranegarAuthenticationSettings>(c.AuthenticationSettingsJson);
        string body;
        try
        {
            var plain = secrets.Unprotect(c.EncryptedAuthenticationSettings);
            if (c.LegacyLoginPayload) body = plain;
            else
            {
                var credentials = ErpSettingsJson.Read<ErpUsernamePassword>(plain);
                if (string.IsNullOrWhiteSpace(credentials.Username) || string.IsNullOrEmpty(credentials.Password))
                    throw new ErpIntegrationException("login_not_configured");
                var fields = new Dictionary<string, string> { [s.UsernameField] = credentials.Username, [s.PasswordField] = credentials.Password };
                if (!string.IsNullOrWhiteSpace(s.FiscalYearField)) fields.Add(s.FiscalYearField, s.FiscalYear);
                if (!string.IsNullOrWhiteSpace(s.DistributionCenterField)) fields.Add(s.DistributionCenterField, s.DistributionCenterId);
                body = JsonSerializer.Serialize(fields);
            }
        }
        catch (System.Security.Cryptography.CryptographicException) { throw new ErpIntegrationException("credential_decryption_failed"); }
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoints.Relative(endpoints.ValidateBase(c.BaseUrl), s.LoginPath))
        { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        using var doc = await transport.SendAsync(c, request, ct);
        LoginResponse login;
        try { login = doc.RootElement.Deserialize<LoginResponse>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (JsonException) { throw new ErpIntegrationException("invalid_login_response"); }
        if (login == null || string.IsNullOrWhiteSpace(login.Token) || login.ExpiresAt == null ||
            login.Token.Any(char.IsControl) || login.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new ErpIntegrationException("invalid_login_response");
        try
        {
            var authorization = string.IsNullOrWhiteSpace(s.AuthorizationScheme) ? login.Token
                : new AuthenticationHeaderValue(s.AuthorizationScheme, login.Token).ToString();
            return new ErpAccessToken(authorization, login.ExpiresAt.Value);
        }
        catch (FormatException) { throw new ErpIntegrationException("invalid_login_response"); }
    }
    private sealed class LoginResponse
    {
        public LoginResponse() { }
        public string Token { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
    }
}
