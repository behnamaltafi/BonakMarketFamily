namespace BonakMarket.Core.Messaging;

public enum EmailAttachmentStorageLocation
{
    /// <summary>
    /// Attachment is embedded as Blob
    /// </summary>
    Blob,

    /// <summary>
    /// Attachment is a reference to <see cref="BonakMarket.Core.Content.Media.MediaFile"/>
    /// </summary>
    FileReference,

    /// <summary>
    /// Attachment is located on disk (physical or virtual path)
    /// </summary>
    Path
}