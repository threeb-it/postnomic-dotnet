namespace Postnomic.Client.Abstractions.Models;

/// <summary>
/// The full representation of a blog post as returned by the authoring API
/// (<see cref="IPostnomicAuthoringService"/>), including its content, lifecycle status, and
/// scheduling metadata. Distinct from <see cref="PostnomicPostDetail"/>, which only ever
/// represents a published post as seen by readers.
/// </summary>
public record PostnomicPost
{
    /// <summary>
    /// The public identifier of the post. Pass this to
    /// <see cref="IPostnomicAuthoringService.UpdatePostAsync"/>,
    /// <see cref="IPostnomicAuthoringService.GetPostAsync"/>, and the lifecycle methods.
    /// </summary>
    public required string PublicId { get; init; }

    /// <summary>The display title of the post.</summary>
    public required string Title { get; init; }

    /// <summary>The URL-friendly slug of the post.</summary>
    public required string Slug { get; init; }

    /// <summary>The HTML body content of the post. <see langword="null"/> when not yet saved.</summary>
    public string? Content { get; init; }

    /// <summary>A short excerpt or teaser for the post. <see langword="null"/> when not set.</summary>
    public string? Excerpt { get; init; }

    /// <summary>The URL of the cover image for the post. <see langword="null"/> when not set.</summary>
    public string? CoverImageUrl { get; init; }

    /// <summary>The URL of the thumbnail image for the post. <see langword="null"/> when not set.</summary>
    public string? ThumbnailImageUrl { get; init; }

    /// <summary>The URL of the social-share image for the post. <see langword="null"/> when not set.</summary>
    public string? ShareImageUrl { get; init; }

    /// <summary>The post's current lifecycle status.</summary>
    public required PostnomicPostStatus Status { get; init; }

    /// <summary>The date/time the post is scheduled to publish, if scheduled.</summary>
    public DateTime? ScheduledPublishDate { get; init; }

    /// <summary>The date/time the post is scheduled to unpublish, if scheduled.</summary>
    public DateTime? ScheduledUnpublishDate { get; init; }

    /// <summary>The UTC date/time the post was published, if it has ever been published.</summary>
    public DateTime? PublishedAt { get; init; }

    /// <summary>The UTC date/time the post was unpublished, if applicable.</summary>
    public DateTime? UnpublishedAt { get; init; }

    /// <summary>Whether this post requires reviewer approval before it can be published.</summary>
    public bool ReviewRequired { get; init; }

    /// <summary>Whether comments are enabled on this post.</summary>
    public bool CommentsEnabled { get; init; }

    /// <summary>The UTC date/time from which comments are accepted, if the comment window is limited.</summary>
    public DateTime? CommentsEnabledFrom { get; init; }

    /// <summary>The UTC date/time until which comments are accepted, if the comment window is limited.</summary>
    public DateTime? CommentsEnabledUntil { get; init; }

    /// <summary>The UTC date/time the post was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>The UTC date/time the post was last updated, if it has been updated since creation.</summary>
    public DateTime? UpdatedAt { get; init; }

    /// <summary>The Auth0 identity user ID (sub claim) of the post's author.</summary>
    public required string AuthorIdentityUserId { get; init; }

    /// <summary>The public ID of the blog this post primarily belongs to.</summary>
    public required string PrimaryBlogPublicId { get; init; }

    /// <summary>The absolute canonical URL of the post on its primary blog.</summary>
    public required string CanonicalUrl { get; init; }

    /// <summary>The tags assigned to this post. Never <see langword="null"/>; may be empty.</summary>
    public ICollection<PostnomicTag> Tags { get; init; } = [];

    /// <summary>The categories assigned to this post. Never <see langword="null"/>; may be empty.</summary>
    public ICollection<PostnomicCategory> Categories { get; init; } = [];
}
