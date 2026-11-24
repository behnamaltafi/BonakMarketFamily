using System.Collections.Generic;
using System.Threading.Tasks;
using BonakMarket.Core.Common;
using BonakMarket.Core.Common.Services;

namespace BonakMarket.Core.Tests.Common;

public class TestExchangeRateProvider : IExchangeRateProvider
{
    /// <summary>
    /// Gets currency live rates
    /// </summary>
    /// <param name="exchangeRateCurrencyCode">Exchange rate currency code</param>
    /// <returns>Exchange rates</returns>
    public Task<IList<ExchangeRate>> GetCurrencyLiveRatesAsync(string exchangeRateCurrencyCode)
    {
        return Task.FromResult<IList<ExchangeRate>>(new List<ExchangeRate>());
    }
}