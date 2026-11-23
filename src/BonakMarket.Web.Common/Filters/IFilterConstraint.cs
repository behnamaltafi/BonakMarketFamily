using Microsoft.AspNetCore.Mvc.Filters;

namespace BonakMarket.Web.Filters;

public interface IFilterConstraint : IFilterFactory
{
    bool Match(ActionContext context);
}