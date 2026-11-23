using BonakMarket.Core.Configuration;

namespace BonakMarket.Google.Auth;

public class GoogleExternalAuthSettings : ISettings
{
    public string ConsumerKey { get; set; }
    public string ConsumerSecret { get; set; }
}