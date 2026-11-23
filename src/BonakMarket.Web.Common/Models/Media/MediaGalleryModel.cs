using System.ComponentModel;
using BonakMarket.Core.Content.Media;
using BonakMarket.Web.Modelling;

namespace BonakMarket.Web.Models.Media;

public partial class MediaGalleryModel : ModelBase
{
    [DefaultValue("[]")]
    public IList<MediaFileInfo> Files { get; set; } = [];
    public int GalleryStartIndex { get; set; }

    [DefaultValue(MediaSettings.ThumbnailSizeXs)]
    public int ThumbSize { get; set; } = MediaSettings.ThumbnailSizeXs;

    [DefaultValue(MediaSettings.ThumbnailSizeXl)]
    public int ImageSize { get; set; } = MediaSettings.ThumbnailSizeXl;
    public string FallbackUrl { get; set; }
    public string ThumbFallbackUrl { get; set; }

    public string ModelName { get; set; }
    public string DefaultAlt { get; set; }

    public bool BoxEnabled { get; set; }
    public bool ImageZoomEnabled { get; set; }
    public string ImageZoomType { get; set; }
}