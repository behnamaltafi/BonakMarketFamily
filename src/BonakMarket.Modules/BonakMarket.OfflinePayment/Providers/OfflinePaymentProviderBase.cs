using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Checkout.Payment;
using BonakMarket.Core.Configuration;
using BonakMarket.Core.Stores;
using BonakMarket.Core.Widgets;
using BonakMarket.OfflinePayment.Settings;

namespace BonakMarket.OfflinePayment;

[PaymentMethod(PaymentMethodType.Standard)]
public abstract class OfflinePaymentProviderBase<TSetting> : PaymentMethodBase
    where TSetting : PaymentSettingsBase, ISettings, new()
{
    protected readonly IStoreContext _storeContext;
    protected readonly ISettingFactory _settingFactory;

    protected OfflinePaymentProviderBase(
        IStoreContext storeContext,
        ISettingFactory settingFactory)
    {
        _storeContext = storeContext;
        _settingFactory = settingFactory;
    }

    public override bool RequiresPaymentSelection => false;

    protected virtual Type GetViewComponentType()
        => null;

    public override async Task<(decimal FixedFeeOrPercentage, bool UsePercentage)> GetPaymentFeeInfoAsync(ShoppingCart cart)
    {
        var settings = await _settingFactory.LoadSettingsAsync<TSetting>(_storeContext.CurrentStore.Id);
        return new(settings.AdditionalFee, settings.AdditionalFeePercentage);
    }

    public override Widget GetPaymentInfoWidget()
    {
        var type = GetViewComponentType();
        return type != null ? new ComponentWidget(type) : null;
    }

    public override async Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        var settings = await _settingFactory.LoadSettingsAsync<TSetting>(processPaymentRequest.StoreId);
        var result = new ProcessPaymentResult();

        switch (settings.TransactMode)
        {
            case TransactMode.Pending:
                result.NewPaymentStatus = PaymentStatus.Pending;
                break;
            case TransactMode.Authorize:
                result.NewPaymentStatus = PaymentStatus.Authorized;
                break;
            case TransactMode.Paid:
                result.NewPaymentStatus = PaymentStatus.Paid;
                break;
            default:
                throw new PaymentException(T("Common.Payment.TranactionTypeNotSupported"));
        }

        return result;
    }
}