using Microsoft.AspNetCore.Mvc.ModelBinding;
using BonakMarket.Core.Catalog.Search;
using BonakMarket.Core.Web;

namespace BonakMarket.Web.Models.Search;

public interface ISearchResultModel
{
    CatalogSearchResult SearchResult { get; }
}

// TODO: (mc) (core) This may be obsolete. Remove it when not needed.
//[ModelBinder(typeof(ISearchResultModel))]
public class SearchResultModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var rootModel = bindingContext.HttpContext.RequestServices.GetService<IViewDataAccessor>().ViewData?.Model;

        if (rootModel is ISearchResultModel searchResultModel)
        {
            bindingContext.Result = ModelBindingResult.Success(searchResultModel);
        }

        return Task.CompletedTask;
    }
}