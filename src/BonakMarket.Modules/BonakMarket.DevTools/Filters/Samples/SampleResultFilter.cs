using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using BonakMarket.Core.Localization;
using BonakMarket.Web.Models.Catalog;

namespace BonakMarket.DevTools.Filters.Samples;

internal class SampleResultFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext filterContext)
    {
        Debug.WriteLine("OnResultExecuting");

        var result = filterContext.Result;
        if (result != null)
        {
            var model = ((Controller)filterContext.Controller).ViewData.Model as CategoryModel;
            if (model != null)
            {
                model.Description = new LocalizedValue<string>("Test", null, null);
                // Do something with model here!
                // If you want to use view models from BonakMarket.Web make sure you have added the project reference first.
            }
        }
    }

    public void OnResultExecuted(ResultExecutedContext filterContext)
    {
        Debug.WriteLine("OnResultExecuted");
    }
}