using Microsoft.AspNetCore.Mvc;
using BonakMarket.Core.Search.Facets;
using BonakMarket.Web.Components;

namespace BonakMarket.DevTools.Components;

public class CustomFacetViewComponent : SmartViewComponent
{
    public IViewComponentResult Invoke(FacetGroup facetGroup, string templateName)
    {
        return View(facetGroup);
    }
}