namespace Postnomic.Client.Abstractions.Models;

/// <summary>
/// The full detail of a published blog post including its content, metadata, comment settings,
/// and the approved comment tree.
/// </summary>
public record PostnomicPostDetail
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
    /// The HTML or Markdown body content of the post.
    /// <see langword="null"/> when no content has been saved.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// A short excerpt or teaser for the post.
    /// <see langword="null"/> when no excerpt has been set.
    /// </summary>
    public string? Excerpt { get; init; }

    /// <summary>
    /// The URL of the cover image for the post.
    /// <see langword="null"/> when no cover image has been set.
    /// </summary>
    public string? CoverImageUrl { get; init; }

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
    /// Whether the comment form should be shown for this post at the current time,
    /// taking into account the blog's comment-window settings.
    /// </summary>
    public bool CommentsEnabled { get; init; }

    /// <summary>
    /// Whether new comments must be approved by a moderator before they appear publicly.
    /// </summary>
    public bool CommentRequireModeration { get; init; }

    /// <summary>
    /// Whether the commenter's first name is a required field.
    /// </summary>
    public bool CommentRequireFirstname { get; init; }

    /// <summary>
    /// Whether the commenter's last name is a required field.
    /// </summary>
    public bool CommentRequireLastname { get; init; }

    /// <summary>
    /// Whether the commenter's email address is a required field.
    /// </summary>
    public bool CommentRequireEmail { get; init; }

    /// <summary>
    /// Whether the commenter's phone number is a required field.
    /// </summary>
    public bool CommentRequirePhone { get; init; }

    /// <summary>
    /// Whether a comment subject line is a required field.
    /// </summary>
    public bool CommentRequireSubject { get; init; }

    /// <summary>
    /// The tags associated with this post. Never <see langword="null"/>; may be empty.
    /// </summary>
    public ICollection<PostnomicTag> Tags { get; init; } = [];

    /// <summary>
    /// The categories associated with this post. Never <see langword="null"/>; may be empty.
    /// </summary>
    public ICollection<PostnomicCategory> Categories { get; init; } = [];

    /// <summary>
    /// The tree of approved comments on this post, with replies nested inside each
    /// comment's <see cref="PostnomicComment.Replies"/> collection.
    /// Never <see langword="null"/>; may be empty.
    /// </summary>
    public ICollection<PostnomicComment> Comments { get; init; } = [];
}
