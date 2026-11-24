using BonakMarket.Core.Widgets;

namespace BonakMarket.Core.Search.Facets;

public interface IFacetTemplateProvider
{
    Widget GetTemplateWidget(FacetGroup facetGroup);
}