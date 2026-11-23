#nullable enable

namespace BonakMarket.Core.Messaging;

public interface IEmailRateLimiter
{
    int GetAllowedSendCount(int requestedCount);
}