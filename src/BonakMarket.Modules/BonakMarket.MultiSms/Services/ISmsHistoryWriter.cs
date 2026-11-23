using BonakMarket.Core.Messaging;
using BonakMarket.MultiSms.Configuration;
using BonakMarket.MultiSms.Domain;

namespace BonakMarket.MultiSms.Services;

/// <summary>Storage abstraction so transport/failover can be unit-tested without SQL.</summary>
public interface ISmsHistoryWriter
{
    Task<SmsMessage> BeginAsync(
        SmsSendRequest request, SmsGatewaySettings settings, CancellationToken ct);

    Task RecordAttemptAsync(
        SmsMessage message, string provider, string status, string messageId,
        string errorCategory, DateTime startedOnUtc, int durationMs, CancellationToken ct);
}
