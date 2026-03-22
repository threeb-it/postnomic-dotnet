namespace Postnomic.Client.Abstractions.Models;

/// <summary>
/// A lightweight summary of a published blog post, suitable for rendering in post listing pages
/// and search results.
/// </summary>
public record PostnomicPostSummary
{
    /// <summary>
    /// The URL-friendly slug of the post (e.g. "my-first-post").
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// The display title of the post.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// A short excerpt or teaser for the post. <see langword="null"/> when no excerpt has been set.
    /// </summary>
    public string? Excerpt { get; init; }

    /// <summary>
    /// The URL of the thumbnail image for the post. <see langword="null"/> when no thumbnail has been set.
    /// </summary>
    public string? ThumbnailImageUrl { get; init; }

    /// <summary>
    /// The full display name of the post author.
    /// </summary>
    public required string AuthorName { get; init; }

    /// <summary>
    /// The URL-friendly slug of the post author, used for linking to the author profile page.
    /// </summary>
    public string? AuthorSlug { get; init; }

    /// <summary>
    /// The UTC date and time at which the post was published.
    /// </summary>
    public DateTime PublishedAt { get; init; }

    /// <summary>
    /// The number of approved comments on this post.
    /// </summary>
    public int CommentCount { get; init; }

    /// <summary>
    /// The tags associated with this post. Never <see langword="null"/>; may be empty.
    /// </summary>
    public ICollection<PostnomicTag> Tags { get; init; } = [];

    /// <summary>
    /// The categories associated with this post. Never <see langword="null"/>; may be empty.
    /// </summary>
    public ICollection<PostnomicCategory> Categories { get; init; } = [];
}
