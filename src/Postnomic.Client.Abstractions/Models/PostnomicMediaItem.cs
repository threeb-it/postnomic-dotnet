namespace Postnomic.Client.Abstractions.Models;

/// <summary>
/// A file or folder stored in the blog's media library, as returned by
/// <see cref="IPostnomicAuthoringService.UploadImageAsync"/>.
/// </summary>
public record PostnomicMediaItem
{
    /// <summary>The file or folder name.</summary>
    public required string Name { get; init; }

    /// <summary>The path of the item relative to the blog's storage folder.</summary>
    public required string Path { get; init; }

    /// <summary>Whether this item is a folder rather than a file.</summary>
    public bool IsFolder { get; init; }

    /// <summary>The file size in bytes. <see langword="null"/> for folders.</summary>
    public long? Size { get; init; }

    /// <summary>The file's content type (MIME type). <see langword="null"/> for folders.</summary>
    public string? ContentType { get; init; }

    /// <summary>The UTC date/time the file was last modified. <see langword="null"/> for folders.</summary>
    public DateTimeOffset? LastModified { get; init; }

    /// <summary>
    /// The URL at which the file is served, host-relative (e.g. <c>/media/blob/blog-media/...</c>).
    /// Pass this directly as <see cref="PostnomicCreatePostRequest.CoverImageUrl"/>,
    /// <see cref="PostnomicCreatePostRequest.ThumbnailImageUrl"/>, or
    /// <see cref="PostnomicCreatePostRequest.ShareImageUrl"/> — the reader client
    /// (<see cref="IPostnomicBlogService"/>) resolves relative image URLs against its own
    /// configured <see cref="PostnomicClientOptions.BaseUrl"/> when rendering.
    /// <see langword="null"/> for folders.
    /// </summary>
    public string? Url { get; init; }
}
