namespace Postnomic.Client.Abstractions.Models;

/// <summary>
/// A lightweight post entry used in sidebar widgets such as "Top Commented" or "Most Read",
/// carrying a single numeric metric alongside the post identity.
/// </summary>
public record PostnomicPopularPost
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
    /// The metric value that determined this post's ranking — either the approved comment
    /// count (for top-commented) or the total page-view count (for most-read).
    /// </summary>
    public int Count { get; init; }
}
