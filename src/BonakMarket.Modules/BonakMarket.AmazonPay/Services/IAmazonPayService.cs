using Amazon.Pay.API.WebStore.CheckoutSession;
using BonakMarket.Core.Identity;
using AmazonPayTypes = Amazon.Pay.API.Types;

namespace BonakMarket.AmazonPay.Services;

public interface IAmazonPayService
{
    Task<int> UpdateAccessKeysAsync(string json, int storeId);

    Task<CheckoutAdressResult> CreateAddressAsync(CheckoutSessionResponse session, Customer customer, bool createBillingAddress);
    AmazonPayTypes.Currency GetAmazonPayCurrency(string currencyCode = null);
}