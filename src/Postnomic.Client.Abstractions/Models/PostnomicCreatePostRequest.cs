namespace Postnomic.Client.Abstractions.Models;

/// <summary>
/// The request body used to create a new post via <see cref="IPostnomicAuthoringService.CreatePostAsync"/>.
/// </summary>
public record PostnomicCreatePostRequest
{
    /// <summary>The display title of the post.</summary>
    public required string Title { get; init; }

    /// <summary>The URL-friendly slug of the post. Must be unique within the blog.</summary>
    public required string Slug { get; init; }

    /// <summary>The HTML body content of the post.</summary>
    public string? Content { get; init; }

    /// <summary>A short excerpt or teaser for the post.</summary>
    public string? Excerpt { get; init; }

    /// <summary>
    /// The URL of the cover image, typically obtained from
    /// <see cref="IPostnomicAuthoringService.UploadImageAsync"/>.
    /// </summary>
    public string? CoverImageUrl { get; init; }

    /// <summary>
    /// The URL of the thumbnail image, typically obtained from
    /// <see cref="IPostnomicAuthoringService.UploadImageAsync"/>.
    /// </summary>
    public string? ThumbnailImageUrl { get; init; }

    /// <summary>
    /// The URL of the social-share image, typically obtained from
    /// <see cref="IPostnomicAuthoringService.UploadImageAsync"/>.
    /// </summary>
    public string? ShareImageUrl { get; init; }

    /// <summary>
    /// The Auth0 identity user ID (sub claim) of the post's author. When
    /// <see langword="null"/>, the API defaults the author to the caller identified by the
    /// configured Personal Access Token.
    /// </summary>
    public string? AuthorIdentityUserId { get; init; }

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

    /// <summary>
    /// When <see langword="true"/>, <see cref="IPostnomicAuthoringService.CreatePostAsync"/>
    /// publishes the post immediately after creating it (an extra
    /// <c>POST .../posts/{postId}/publish</c> call the API does not support atomically with
    /// creation) instead of leaving it as a <see cref="PostnomicPostStatus.Draft"/>. If the
    /// publish step fails (e.g. because <see cref="ReviewRequired"/> is set but no reviewers are
    /// assigned), the post has already been created as a draft — the thrown
    /// <see cref="PostnomicApiException"/> does not roll that back. Defaults to
    /// <see langword="false"/>.
    /// </summary>
    public bool PublishImmediately { get; init; }
}
