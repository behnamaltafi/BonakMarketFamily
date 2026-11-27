using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BonakMarket.Core.Checkout.Cart;
using BonakMarket.Core.Data;
using BonakMarket.Core.Identity;
using BonakMarket.Engine.Initialization;
using BonakMarket.Events;
using BonakMarket.Scheduling;
using BonakMarket.StorefrontApi.Domain;
using BonakMarket.StorefrontApi.Security;

namespace BonakMarket.StorefrontApi.Services;

// Preserve live empty API guest carts despite native DeleteGuestsTask.
internal sealed class GuestCartDeletionConsumer(SmartDbContext db) : IConsumer
{
    public void HandleEvent(GuestCustomerDeletingEvent message)
    {
        // Protect even expired/merged rows until the locked cleanup task removes the token.
        // Otherwise a native cleanup could delete an empty cart during an in-flight request.
        message.Query = message.Query.Where(c => !db.Set<GuestCartToken>().Any(t => t.CustomerId == c.Id));
    }
}
