using System.Net;
using Amazon.Pay.API.Types;
using BonakMarket.Core.Checkout.Payment;
using BonakMarket.Http;
using BonakMarket.Web.Controllers;

namespace BonakMarket.AmazonPay.Services;

public class AmazonPayException : PaymentException
{
    public AmazonPayException(string message)
        : this(message, (Exception)null)
    {
    }

    public AmazonPayException(string message, Exception innerException)
        : base(message, innerException, AmazonPayProvider.SystemName)
    {
        RedirectRoute = CreateRouteInfo();
    }

    public AmazonPayException(AmazonPayResponse response)
        : this(response.GetShortMessage(), response)
    {
    }

    /// <param name="message">Buyer friendly error message.</param>
    /// <param name="response">AmazonPay API response.</param>
    /// <remarks>
    /// The full error message is encapsulated via an inner exception
    /// so that the buyer does not see these details that are meaningless to him.
    /// </remarks>
    public AmazonPayException(string message, AmazonPayResponse response)
        : base(message,
              new PaymentResponse((HttpStatusCode)response.Status, response.Headers),
              new Exception(response.GetFullMessage()),
              AmazonPayProvider.SystemName)
    {
        RedirectRoute = CreateRouteInfo();
    }

    private RouteInfo CreateRouteInfo()
        => new(nameof(ShoppingCartController.Cart), "ShoppingCart", (object)null);
}