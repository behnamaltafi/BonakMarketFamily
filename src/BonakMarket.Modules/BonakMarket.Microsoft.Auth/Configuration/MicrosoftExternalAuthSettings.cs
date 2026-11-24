using BonakMarket.Core.Configuration;

namespace BonakMarket.Microsoft.Auth;

public class MicrosoftExternalAuthSettings : ISettings
{
    public string ClientKeyIdentifier { get; set; }
    public string ClientSecret { get; set; }
}