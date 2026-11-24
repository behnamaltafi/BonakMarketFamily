using BonakMarket.Core.Configuration;

namespace BonakMarket.Twitter.Auth;

public class TwitterExternalAuthSettings : ISettings
{
    public string ConsumerKey { get; set; }
    public string ConsumerSecret { get; set; }
}