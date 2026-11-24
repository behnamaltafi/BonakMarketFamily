using Microsoft.AspNetCore.Http;
using BonakMarket.Core.Seo.Routing;

namespace BonakMarket.Core.Seo;

/// <summary>
/// Responsible for applying URL rules to current request url (HTTPS, canonical host name, culture code etc.)
/// </summary>
public interface IUrlFilter
{
    void Apply(UrlPolicy policy, HttpContext httpContext);
}