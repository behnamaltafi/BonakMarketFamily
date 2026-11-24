using BonakMarket.Caching;
using BonakMarket.ComponentModel;
using BonakMarket.Core.Security;
using BonakMarket.Core.Stores;
using BonakMarket.Web.Models.Pdf;

namespace BonakMarket.Web.Controllers;

public class PdfController : Controller
{
    private readonly ICacheFactory _cacheFactory;
    private readonly IStoreContext _storeContext;

    public PdfController(ICacheFactory cacheFactory, IStoreContext storeContext)
    {
        _cacheFactory = cacheFactory;
        _storeContext = storeContext;
    }

    [NeverAuthorize]
    public async Task<IActionResult> ReceiptHeader(PdfSectionVariables vars, int storeId = 0, bool isPartial = false)
    {
        var model = await PreparePdfReceiptSectionModelAsync(storeId);

        ViewData["SectionVars"] = vars;
        ViewData["IsPartial"] = isPartial;

        return isPartial ? PartialView(model) : View(model);
    }

    [NeverAuthorize]
    public async Task<IActionResult> ReceiptFooter(PdfSectionVariables vars, int storeId = 0, bool isPartial = false)
    {
        var model = await PreparePdfReceiptSectionModelAsync(storeId);

        ViewData["SectionVars"] = vars;
        ViewData["IsPartial"] = isPartial;

        return isPartial ? PartialView(model) : View(model);
    }

    private async Task<PdfReceiptSectionModel> PreparePdfReceiptSectionModelAsync(int storeId)
    {
        return await _cacheFactory.GetMemoryCache().GetAsync($"PdfReceiptSectionModel-{storeId}", async (o) =>
        {
            // 1 min. (just for the duration of pdf processing)
            o.ExpiresIn(TimeSpan.FromMinutes(1));

            var store = _storeContext.GetStoreById(storeId) ?? _storeContext.CurrentStore;
            var mapper = MapperFactory.GetMapper<Store, PdfReceiptSectionModel>();

            return await mapper.MapAsync(store);
        });
    }
}