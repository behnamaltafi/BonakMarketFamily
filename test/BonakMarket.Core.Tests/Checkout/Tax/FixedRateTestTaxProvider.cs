using System.Threading.Tasks;
using BonakMarket.Core.Checkout.Tax;

namespace BonakMarket.Core.Tests.Tax;

public class FixedRateTestTaxProvider : ITaxProvider
{
    public Task<TaxRate> GetTaxRateAsync(TaxRateRequest request)
    {
        return Task.FromResult(new TaxRate(10, request.TaxCategoryId));
    }
}