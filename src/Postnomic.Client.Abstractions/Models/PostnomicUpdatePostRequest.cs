namespace Postnomic.Client.Abstractions.Models;

/// <summary>
/// The request body used to update an existing post via
/// <see cref="IPostnomicAuthoringService.UpdatePostAsync"/>. This is a full replace of the
/// post's editable fields — omitted collections (<see cref="TagSlugs"/>,
/// <see cref="CategorySlugs"/>) clear the existing assignments, matching the API's behaviour.
/// </summary>
public record PostnomicUpdatePostRequest
{
    /// <summary>The display title of the post.</summary>
    public required string Title { get; init; }

    /// <summary>The URL-friendly slug of the post. Must be unique within the blog.</summary>
    public required string Slug { get; init; }

    /// <summary>The HTML body content of the post.</summary>
    public string? Content { get; init; }

    /// <summary>A short excerpt or teaser for the post.</summary>
    public string? Excerpt { get; init; }

    /// <summary>The URL of the cover image.</summary>
    public string? CoverImageUrl { get; init; }

    /// <summary>The URL of the thumbnail image.</summary>
    public string? ThumbnailImageUrl { get; init; }

    /// <summary>The URL of the social-share image.</summary>
    public string? ShareImageUrl { get; init; }

    /// <summary>Whether this post must be approved by a reviewer before it can be published.</summary>
    public bool ReviewRequired { get; init; }

    /// <summary>Whether comments are enabled on this post.</summary>
    public bool CommentsEnabled { get; init; }

    /// <summary>The UTC date/time from which comments are accepted, if the comment window is limited.</summary>
    public DateTime? CommentsEnabledFrom { get; init; }

    /// <summary>The UTC date/time until which comments are accepted, if the comment window is limited.</summary>
    public DateTime? CommentsEnabledUntil { get; init; }

    /// <summary>The slugs of the tags to assign to this post. Unknown slugs are created automatically.</summary>
    public ICollection<string> TagSlugs { get; init; } = [];

    /// <summary>The slugs of the categories to assign to this post. Unknown slugs are ignored by the API.</summary>
    public ICollection<string> CategorySlugs { get; init; } = [];
}
