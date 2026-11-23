using BonakMarket.Core.Widgets;

namespace BonakMarket.Core.Search.Facets;

public interface IFacetTemplateSelector : IOrdered
{
    Widget GetTemplateWidget(FacetGroup facetGroup);
}