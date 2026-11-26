namespace BonakMarket.ERP.Authentication;
public sealed class VaranegarAuthenticationSettings
{
    public string LoginPath { get; set; } = "api/auth/login";
    public string AuthorizationScheme { get; set; } = "Bearer";
    // Wire names are defaults only until confirmed against the live API.
    public string UsernameField { get; set; } = "username";
    public string PasswordField { get; set; } = "password";
    public string FiscalYearField { get; set; }
    public string FiscalYear { get; set; }
    public string DistributionCenterField { get; set; }
    public string DistributionCenterId { get; set; }
}
public sealed class ErpUsernamePassword
{
    public string Username { get; set; }
    public string Password { get; set; }
}
