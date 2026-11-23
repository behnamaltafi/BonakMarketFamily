using BonakMarket.Utilities;
using BonakMarket.Web.TagHelpers.Shared;

namespace BonakMarket.Web.Components;

public class FileUploaderViewComponent : SmartViewComponent
{
    public IViewComponentResult Invoke(FileUploaderModel model)
    {
        if (model == null)
        {
            return Empty();
        }

        if (model.UploadText.IsEmpty())
        {
            model.UploadText = T("Common.FileUploader.Upload");
        }

        if (model.Name.IsEmpty())
        {
            model.Name = "upload-" + CommonHelper.GenerateRandomInteger();
        }

        return View(model);
    }
}