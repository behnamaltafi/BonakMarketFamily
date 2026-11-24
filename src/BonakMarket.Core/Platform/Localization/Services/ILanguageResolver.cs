using Microsoft.AspNetCore.Http;
using BonakMarket.Core.Identity;

namespace BonakMarket.Core.Localization;

/// <summary>
/// Responsible for resolving the current customer's working language.
/// </summary>
public interface ILanguageResolver
{
    Language ResolveLanguage(Customer currentCustomer, HttpContext httpContext);
    Task<Language> ResolveLanguageAsync(Customer currentCustomer, HttpContext httpContext);
}