using BonakMarket.Core.Checkout.Payment;

namespace BonakMarket.Admin.Models.Payments;

[LocalizedDisplay("Admin.Configuration.Settings.Payment.")]
public class PaymentSettingsModel : ModelBase
{
    [LocalizedDisplay("*CapturePaymentReason")]
    public CapturePaymentReason? CapturePaymentReason { get; set; }

    [LocalizedDisplay("*ProductDetailPaymentMethodSystemNames")]
    public string[] ProductDetailPaymentMethodSystemNames { get; set; }

    [LocalizedDisplay("*DisplayPaymentMethodIcons")]
    public bool DisplayPaymentMethodIcons { get; set; }

    [LocalizedDisplay("*SkipPaymentSelectionIfSingleOption")]
    public bool SkipPaymentSelectionIfSingleOption { get; set; }
}