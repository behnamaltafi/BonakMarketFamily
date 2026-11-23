using BonakMarket.IO;

namespace BonakMarket.Core.Content.Media;

/// <summary>
/// Storage abstraction for media files.
/// </summary>
public interface IMediaFileSystem : IFileSystem
{
    /// <summary>
    /// Contains media storage and path configuration.
    /// </summary>
    IMediaStorageConfiguration StorageConfiguration { get; }

    /// <summary>
    /// Checks whether the underlying storage is remote, like 'Azure' for example. 
    /// </summary>
    bool IsCloudStorage { get; }
}