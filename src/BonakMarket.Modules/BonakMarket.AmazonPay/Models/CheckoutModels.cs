using BonakMarket.Core.Common;

namespace BonakMarket.AmazonPay.Models;

public class CheckoutReviewResult
{
    public bool Success { get; set; }
    public bool IsShippingMethodMissing { get; set; }
    public bool RequiresAddressUpdate { get; set; }
}

public class CheckoutAdressResult
{
    public bool Success { get; set; }
    public bool IsCountryAllowed { get; set; } = true;
    public string CountryCode { get; set; } = string.Empty;
    public Address Address { get; set; }
}