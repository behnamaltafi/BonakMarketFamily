using BonakMarket.Core.Content.Media;

namespace BonakMarket.Core.Data;

public partial class SmartDbContext
{
    public DbSet<MediaFile> MediaFiles { get; set; }
    public DbSet<MediaFolder> MediaFolders { get; set; }
    public DbSet<MediaAlbum> MediaAlbums { get; set; }
    public DbSet<MediaTrack> MediaTracks { get; set; }
    public DbSet<MediaTag> MediaTags { get; set; }
    public DbSet<MediaStorage> MediaStorage { get; set; }
    public DbSet<Download> Downloads { get; set; }
}