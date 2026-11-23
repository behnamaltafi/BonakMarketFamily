using BonakMarket.AmazonPay.Components;
using BonakMarket.Core.Identity;
using BonakMarket.Core.Widgets;
using BonakMarket.Engine.Modularity;

namespace BonakMarket.AmazonPay.Providers;

[SystemName("BonakMarket.AmazonPay")]
[FriendlyName("Amazon sign-in")]
[Order(-1)]
public class AmazonPaySignInProvider : IExternalAuthenticationMethod
{
    // Keep old provider name for compatibility (see ExternalAuthenticationRecord.ProviderSystemName).
    public static string SystemName => "BonakMarket.AmazonPay";

    public Widget GetDisplayWidget(int storeId)
        => new ComponentWidget(typeof(SignInButtonViewComponent));
}