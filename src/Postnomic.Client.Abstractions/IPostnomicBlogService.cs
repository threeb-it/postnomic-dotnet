using Postnomic.Client.Abstractions.Models;

namespace Postnomic.Client.Abstractions;

/// <summary>
/// Provides access to all public Postnomic blog API endpoints for a single, pre-configured blog.
/// The blog slug is resolved from <see cref="PostnomicClientOptions.BlogSlug"/> so callers do not
/// need to pass it explicitly on every call.
/// </summary>
public interface IPostnomicBlogService
{
    /// <summary>
    /// Retrieves public metadata (name, slug, description) for the configured blog.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="PostnomicBlogInfo"/> when the blog exists, or <see langword="null"/>
    /// when the API returns a 404 response.
    /// </returns>
    Task<PostnomicBlogInfo?> GetBlogAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all tags that are used by at least one published post on the configured blog,
    /// ordered alphabetically by name.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A list of <see cref="PostnomicTag"/> objects. The list is empty when no tags exist.
    /// </returns>
    Task<List<PostnomicTag>> GetTagsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all categories that are used by at least one published post on the configured
    /// blog, ordered alphabetically by name.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A list of <see cref="PostnomicCategory"/> objects. The list is empty when no categories exist.
    /// </returns>
    Task<List<PostnomicCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all authors who have at least one published post on the configured blog,
    /// ordered alphabetically by name.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A list of <see cref="PostnomicAuthor"/> objects. The list is empty when no authors exist.
    /// </returns>
    Task<List<PostnomicAuthor>> GetAuthorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the full profile of a single author by their URL-friendly slug.
    /// </summary>
    /// <param name="authorSlug">The author's URL-friendly slug.</param>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="PostnomicAuthorProfile"/> when the author exists and has published posts,
    /// or <see langword="null"/> when the API returns a 404 response.
    /// </returns>
    Task<PostnomicAuthorProfile?> GetAuthorProfileAsync(
        string authorSlug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated list of published posts for the configured blog. Supports optional
    /// filtering by tag slug, category slug, author display name, and a full-text search term.
    /// </summary>
    /// <param name="page">The 1-based page number to retrieve. Defaults to <c>1</c>.</param>
    /// <param name="pageSize">
    /// The number of posts per page. The API enforces a maximum of <c>50</c>.
    /// Defaults to <c>5</c>.
    /// </param>
    /// <param name="tag">
    /// Optional tag slug to filter posts by. Pass <see langword="null"/> to omit the filter.
    /// </param>
    /// <param name="category">
    /// Optional category slug to filter posts by. Pass <see langword="null"/> to omit the filter.
    /// </param>
    /// <param name="author">
    /// Optional author display name to filter posts by. Pass <see langword="null"/> to omit the filter.
    /// </param>
    /// <param name="search">
    /// Optional search term matched against post titles, excerpts, and content.
    /// Pass <see langword="null"/> to omit the filter.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="PostnomicPagedResult{T}"/> containing the matching
    /// <see cref="PostnomicPostSummary"/> items and paging metadata.
    /// </returns>
    Task<PostnomicPagedResult<PostnomicPostSummary>> GetPostsAsync(
        int page = 1,
        int pageSize = 5,
        string? tag = null,
        string? category = null,
        string? author = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the full detail of a single published post including its content, comment
    /// settings, and the approved comment tree.
    /// </summary>
    /// <param name="postSlug">The URL-friendly slug of the post to retrieve.</param>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="PostnomicPostDetail"/> when the post exists and is published, or
    /// <see langword="null"/> when the API returns a 404 response.
    /// </returns>
    Task<PostnomicPostDetail?> GetPostAsync(
        string postSlug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a new comment on a published post. Which fields of
    /// <paramref name="request"/> are required at runtime depends on the blog's comment
    /// settings available via <see cref="PostnomicPostDetail"/>.
    /// </summary>
    /// <param name="postSlug">The URL-friendly slug of the post to comment on.</param>
    /// <param name="request">The comment payload.</param>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// The newly created <see cref="PostnomicComment"/> when the request is accepted, or
    /// <see langword="null"/> when the API returns a non-success response (e.g. 400 or 404).
    /// </returns>
    Task<PostnomicComment?> CreateCommentAsync(
        string postSlug,
        PostnomicCreateCommentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the top posts on the configured blog ranked by approved comment count,
    /// ordered descending.
    /// </summary>
    /// <param name="count">
    /// The number of posts to return. The API enforces a range of 1–10. Defaults to <c>3</c>.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A list of <see cref="PostnomicPopularPost"/> objects sorted by comment count descending.
    /// The list is empty when no published posts exist.
    /// </returns>
    Task<List<PostnomicPopularPost>> GetTopCommentedPostsAsync(
        int count = 3,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the most-read posts on the configured blog ranked by total page-view count,
    /// ordered descending.
    /// </summary>
    /// <param name="count">
    /// The number of posts to return. The API enforces a range of 1–10. Defaults to <c>3</c>.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A list of <see cref="PostnomicPopularPost"/> objects sorted by view count descending.
    /// The list is empty when no published posts exist or no analytics data has been recorded.
    /// </returns>
    Task<List<PostnomicPopularPost>> GetMostReadPostsAsync(
        int count = 3,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a page-view event for the configured blog. Call this once when a page loads.
    /// The server captures the visitor's session, the post being viewed (if any), and the
    /// HTTP referrer for analytics purposes.
    /// </summary>
    /// <param name="sessionId">
    /// A client-generated UUID that identifies the visitor's browser session across multiple
    /// page views and duration updates.
    /// </param>
    /// <param name="postSlug">
    /// The URL-friendly slug of the post being viewed. Pass <see langword="null"/> for the
    /// blog index page or any non-post page.
    /// </param>
    /// <param name="referrer">
    /// The value of the HTTP <c>Referer</c> header (where the visitor came from).
    /// Pass <see langword="null"/> when there is no referrer.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the asynchronous operation.
    /// </param>
    Task RecordPageViewAsync(
        string sessionId,
        string? postSlug = null,
        string? referrer = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the read duration of the most recent page-view recorded for the given session.
    /// Call this on the <c>beforeunload</c> event or periodically via a keep-alive beacon to
    /// capture how long the visitor spent on the page.
    /// </summary>
    /// <param name="sessionId">
    /// The session ID that was passed to <see cref="RecordPageViewAsync"/> when the page view
    /// was first recorded.
    /// </param>
    /// <param name="durationSeconds">
    /// The estimated number of seconds the visitor has spent on the page.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that can be used to cancel the asynchronous operation.
    /// </param>
    Task UpdateReadDurationAsync(
        string sessionId,
        int durationSeconds,
        CancellationToken cancellationToken = default);
}
