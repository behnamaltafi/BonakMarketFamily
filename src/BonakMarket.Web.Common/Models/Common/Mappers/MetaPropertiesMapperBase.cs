using Microsoft.AspNetCore.Http;
using BonakMarket.ComponentModel;
using BonakMarket.Core.Common.Configuration;
using BonakMarket.Core.Content.Media;
using BonakMarket.Core.Stores;
using BonakMarket.Http;
using BonakMarket.Web.Modelling;

namespace BonakMarket.Web.Models.Common.Mappers;

public abstract class MetaPropertiesMapperBase<TFrom> : IMapper<TFrom, MetaPropertiesModel>
    where TFrom : EntityModelBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    protected MetaPropertiesMapperBase(IHttpContextAccessor httpContextAccessor)
    {
        Guard.NotNull(httpContextAccessor.HttpContext, nameof(httpContextAccessor.HttpContext));

        _httpContextAccessor = httpContextAccessor;
    }

    protected HttpContext HttpContext
    {
        get => _httpContextAccessor.HttpContext;
    }

    public async Task MapAsync(TFrom from, MetaPropertiesModel to, dynamic parameters = null)
    {
        await MapCoreAsync(from, to, parameters);
        var fileInfo = GetMediaFile(from);

        PrepareMetaPropertiesModel(to, fileInfo);
    }

    protected abstract MediaFileInfo GetMediaFile(TFrom from);

    protected abstract Task MapCoreAsync(TFrom source, MetaPropertiesModel model, dynamic parameters = null);

    protected void PrepareMetaPropertiesModel(MetaPropertiesModel model, MediaFileInfo fileInfo)
    {
        var request = HttpContext.Request;
        var services = HttpContext.RequestServices;
        var storeContext = services.GetRequiredService<IStoreContext>();
        var urlHelper = services.GetRequiredService<IUrlHelper>();
        var socialSettings = services.GetRequiredService<SocialSettings>();

        model.Site = urlHelper.RouteUrl("Homepage", null, request.Scheme);
        model.SiteName = storeContext.CurrentStore.Name;

        var imageUrl = fileInfo?.GetUrl();
        if (fileInfo != null && imageUrl.HasValue())
        {
            imageUrl = WebHelper.GetAbsoluteUrl(imageUrl, request, true);
            model.ImageUrl = imageUrl;
            model.ImageType = fileInfo.MimeType;

            if (fileInfo.Alt.HasValue())
            {
                model.ImageAlt = fileInfo.Alt;
            }

            if (fileInfo.Size.Width > 0 && fileInfo.Size.Height > 0)
            {
                model.ImageWidth = fileInfo.Size.Width;
                model.ImageHeight = fileInfo.Size.Height;
            }
        }

        model.TwitterSite = socialSettings.TwitterSite;
        model.FacebookAppId = socialSettings.FacebookAppId;
    }
}