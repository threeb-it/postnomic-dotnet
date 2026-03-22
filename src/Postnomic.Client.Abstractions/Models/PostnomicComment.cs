namespace Postnomic.Client.Abstractions.Models;

/// <summary>
/// Represents a single approved comment on a blog post. Comments may be nested; top-level
/// comments carry their replies in the <see cref="Replies"/> collection.
/// </summary>
public record PostnomicComment
{
    /// <summary>
    /// The public identifier of the comment, used when submitting a reply via
    /// <see cref="PostnomicCreateCommentRequest.ParentCommentPublicId"/>.
    /// </summary>
    public required string PublicId { get; init; }

    /// <summary>
    /// The optional subject line of the comment. <see langword="null"/> when no subject was provided.
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// The body text of the comment.
    /// </summary>
    public required string Body { get; init; }

    /// <summary>
    /// The display name of the commenter. <see langword="null"/> when the author submitted
    /// the comment anonymously or without providing a name.
    /// </summary>
    public string? AuthorName { get; init; }

    /// <summary>
    /// The UTC date and time at which the comment was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Direct replies to this comment. Never <see langword="null"/>; may be empty.
    /// </summary>
    public ICollection<PostnomicComment> Replies { get; init; } = [];
}
