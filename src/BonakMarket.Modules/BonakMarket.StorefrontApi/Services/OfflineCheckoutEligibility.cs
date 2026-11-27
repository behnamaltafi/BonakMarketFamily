#nullable enable
using BonakMarket.Core.Checkout.Attributes;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Checkout.Orders;
using BonakMarket.Core.Checkout.Orders.Handlers;
using BonakMarket.Core.Checkout.Payment;
using BonakMarket.Core.Common;
using BonakMarket.Core.Identity;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Services;

public sealed class OfflineCheckoutEligibility(StorefrontRequestContext request, ShoppingCartSettings cartSettings,
    ICheckoutFactory checkoutFactory, ICheckoutAttributeMaterializer checkoutAttributes, IPaymentService payments)
{
    internal async Task<bool> SupportsAsync(ShoppingCart native, CheckoutDraft draft)
    {
        var ga = request.Customer.GenericAttributes;
        if (!native.HasItems || native.ContainsRecurringItem() || native.Requirements != CheckoutRequirements.All ||
            cartSettings.CheckoutProcess != CheckoutProcess.Standard || cartSettings.ShowConfirmOrderLegalHint ||
            cartSettings.ThirdPartyEmailHandOver != CheckoutThirdPartyEmailHandOver.None ||
            ga.UseCreditBalanceDuringCheckout != 0 || ga.UseRewardPointsDuringCheckout || ga.GiftCardCouponCodes.Any() ||
            native.Items.Any(x => x.Item.Product == null || x.Item.Product.IsEsd || x.Item.Product.IsGiftCard))
            return false;
        var handlers = new[] { typeof(BillingAddressHandler), typeof(ShippingAddressHandler), typeof(ShippingMethodHandler),
            typeof(PaymentMethodHandler), typeof(ConfirmHandler) };
        if (!checkoutFactory.GetCheckoutSteps().Select(x => x.Handler.Metadata.HandlerType).SequenceEqual(handlers))
            return false;
        if ((await checkoutAttributes.GetCheckoutAttributesAsync(native, request.StoreId)).Count > 0) return false;
        if (draft.Payment == null) return false;
        var provider = await payments.LoadPaymentProviderBySystemNameAsync(draft.Payment.SystemName, true, request.StoreId);
        return provider != null && !provider.Value.RequiresInteraction && !provider.Value.RequiresConfirmation &&
            OfflineCheckoutPolicy.IsSupported(provider.Metadata.SystemName, provider.Value.GetType().FullName,
                provider.Metadata.ModuleDescriptor?.SystemName) &&
            await payments.IsPaymentProviderActiveAsync(provider.Metadata.SystemName, native, request.StoreId);
    }
}
