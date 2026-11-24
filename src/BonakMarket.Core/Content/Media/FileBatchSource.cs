#nullable enable

using BonakMarket.Core.Content.Media.Storage;
using BonakMarket.Core.DataExchange.Import;
using BonakMarket.Net.Http;

namespace BonakMarket.Core.Content.Media;

public class FileBatchSource : Disposable
{
    public FileBatchSource(MediaStorageItem source)
    {
        Source = Guard.NotNull(source);
    }

    /// <summary>
    /// The file source as <see cref="MediaStorageItem"/>.
    /// </summary>
    public MediaStorageItem Source { get; }

    /// <summary>
    /// Name of file including extension.
    /// </summary>
    public string FileName { get; init; } = default!;

    /// <summary>
    /// Any state to identify the source later after batch save. E.g.: <see cref="ImportRow{T}"/>, <see cref="DownloadManagerItem"/> etc.
    /// </summary>
    public object? State { get; init; }

    protected override void OnDispose(bool disposing)
    {
        if (disposing) Source.Dispose();
    }
}

public class FileBatchResult
{
    public FileBatchSource Source { get; init; } = default!;
    public MediaFileInfo? File { get; init; }
    public MediaPathData? PathData { get; init; }
    public Exception? Exception { get; set; }
    public bool IsDuplicate { get; set; }
    public string? UniquePath { get; set; }
    internal MediaStorageItem? StorageItem { get; set; }
}