namespace Postnomic.Client.Abstractions.Models;

/// <summary>
/// The request body used to create a new comment on a blog post via the public API.
/// Which fields are required at runtime is determined by the blog's comment settings exposed
/// in <see cref="PostnomicPostDetail"/>.
/// </summary>
public record PostnomicCreateCommentRequest
{
    /// <summary>
    /// The <see cref="PostnomicComment.PublicId"/> of the comment being replied to.
    /// Pass <see langword="null"/> to create a top-level comment.
    /// </summary>
    public string? ParentCommentPublicId { get; init; }

    /// <summary>
    /// An optional subject line for the comment. Required when
    /// <see cref="PostnomicPostDetail.CommentRequireSubject"/> is <see langword="true"/>.
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// The body text of the comment. Always required.
    /// </summary>
    public required string Body { get; init; }

    /// <summary>
    /// The commenter's first name. Required when
    /// <see cref="PostnomicPostDetail.CommentRequireFirstname"/> is <see langword="true"/>.
    /// </summary>
    public string? AuthorFirstname { get; init; }

    /// <summary>
    /// The commenter's last name. Required when
    /// <see cref="PostnomicPostDetail.CommentRequireLastname"/> is <see langword="true"/>.
    /// </summary>
    public string? AuthorLastname { get; init; }

    /// <summary>
    /// The commenter's email address. Required when
    /// <see cref="PostnomicPostDetail.CommentRequireEmail"/> is <see langword="true"/>.
    /// </summary>
    public string? AuthorEmail { get; init; }

    /// <summary>
    /// The commenter's phone number. Required when
    /// <see cref="PostnomicPostDetail.CommentRequirePhone"/> is <see langword="true"/>.
    /// </summary>
    public string? AuthorPhone { get; init; }
}
