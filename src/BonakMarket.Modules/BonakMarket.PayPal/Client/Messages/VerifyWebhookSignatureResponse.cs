#nullable enable

namespace BonakMarket.PayPal.Client.Messages;

public class VerifyWebhookSignatureResponse
{
    public string? VerificationStatus { get; set; }
}
