using BonakMarket.Core.Messaging.Events;
using BonakMarket.Events;

namespace BonakMarket;

public static class NewsletterSubscriptionEventPublisherExtensions
{
    /// <summary>
    /// Publishes the newsletter subscribed event.
    /// </summary>
    /// <param name="eventPublisher">The event publisher.</param>
    /// <param name="email">The mail address which has subscribed.</param>
    public static Task PublishNewsletterSubscribedAsync(this IEventPublisher eventPublisher, string email)
    {
        return email.HasValue()
            ? eventPublisher.PublishAsync(new NewsletterSubscribedEvent(email))
            : Task.CompletedTask;
    }

    /// <summary>
    /// Publishes the newsletter unsubscribed event.
    /// </summary>
    /// <param name="eventPublisher">The event publisher.</param>
    /// <param name="email">The mail address which has unsubscribed.</param>
    public static Task PublishNewsletterUnsubscribedAsync(this IEventPublisher eventPublisher, string email)
    {
        return email.HasValue()
            ? eventPublisher.PublishAsync(new NewsletterUnsubscribedEvent(email))
            : Task.CompletedTask;
    }
}