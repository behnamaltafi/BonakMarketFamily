using Microsoft.AspNetCore.DataProtection;

namespace BonakMarket.ERP.Services;
public sealed class ErpSecrets
{
    private readonly IDataProtector _protector;
    public ErpSecrets(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("BonakMarket.ERP.Connection.Login.v1");
    public string Protect(string value) => _protector.Protect(value);
    public string Unprotect(string value) => _protector.Unprotect(value);
}
