using BonakMarket.Core.Configuration;

namespace BonakMarket.Facebook.Auth;

public class FacebookExternalAuthSettings : ISettings
{
    public string ClientKeyIdentifier { get; set; }
    public string ClientSecret { get; set; }
}