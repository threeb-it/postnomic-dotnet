using Postnomic.Client.Abstractions.Models;

namespace Postnomic.Client.Abstractions;

/// <summary>
/// Provides write access to a single, pre-configured blog's posts and media library — creating,
/// updating, publishing, and archiving posts, and uploading images. This is the authoring
/// counterpart to the read-only <see cref="IPostnomicBlogService"/>; most consumers only need
/// that interface, so authoring is kept on its own registration
/// (<c>AddPostnomicAuthoringClient</c>) and its own credential.
/// </summary>
/// <remarks>
/// <para>
/// Authoring requires a <b>Personal Access Token</b> (<see cref="PostnomicClientOptions.PersonalAccessToken"/>,
/// format <c>pnp_...</c>), minted from the Postnomic dashboard's "Access Tokens" page and sent as
/// <c>Authorization: Bearer &lt;token&gt;</c>. The read-only <c>X-Api-Key</c>
/// (<see cref="PostnomicClientOptions.ApiKey"/>) used by <see cref="IPostnomicBlogService"/> is
/// scoped to anonymous, read-only access to published content and cannot authenticate any of
/// these calls — the API rejects it with 401.
/// </para>
/// <para>
/// Every call is scoped to <see cref="PostnomicClientOptions.BlogId"/> — the blog's public ID
/// (a GUID, as shown in the dashboard URL or returned by the management API's <c>BlogResponse</c>).
/// This is <b>not</b> the same value as <see cref="PostnomicClientOptions.BlogSlug"/>, which the
/// reader client uses; the authoring API's routes key on the blog's public ID.
/// </para>
/// <para>
/// The token's owner must be a member of the target blog with at least the <c>Author</c> role
/// (for create/update/submit-for-review/media-upload) or <c>Editor</c> (for publish/unpublish/
/// archive). A rejection surfaces as <see cref="PostnomicApiException"/>, carrying the API's
/// HTTP status code and rejection reason.
/// </para>
/// </remarks>
public interface IPostnomicAuthoringService
{
    /// <summary>
    /// Creates a new post on the configured blog. The post is created as a
    /// <see cref="PostnomicPostStatus.Draft"/> unless
    /// <see cref="PostnomicCreatePostRequest.PublishImmediately"/> is set.
    /// </summary>
    /// <param name="request">The post to create.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>The newly created <see cref="PostnomicPost"/>.</returns>
    /// <exception cref="PostnomicApiException">
    /// The API rejected the request — e.g. 409 when the slug is already taken on this blog, 403
    /// when a subscription quota (post count) is exhausted, or 403 when the token's owner lacks
    /// the <c>Author</c> role on the blog.
    /// </exception>
    Task<PostnomicPost> CreatePostAsync(
        PostnomicCreatePostRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces an existing post's content and metadata. This is a full replace of the editable
    /// fields, not a partial patch — see <see cref="PostnomicUpdatePostRequest"/>.
    /// </summary>
    /// <param name="postId">The public ID of the post to update.</param>
    /// <param name="request">The post's new content and metadata.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>The updated <see cref="PostnomicPost"/>.</returns>
    /// <exception cref="PostnomicApiException">
    /// The API rejected the request — e.g. 404 when no such post exists on this blog, or 409
    /// when the new slug collides with another post.
    /// </exception>
    Task<PostnomicPost> UpdatePostAsync(
        string postId,
        PostnomicUpdatePostRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single post by its public ID, regardless of its lifecycle status — unlike
    /// <see cref="IPostnomicBlogService.GetPostAsync"/>, which only ever returns published posts.
    /// Use this to verify what a prior <see cref="CreatePostAsync"/> or <see cref="UpdatePostAsync"/>
    /// call actually wrote.
    /// </summary>
    /// <param name="postId">The public ID of the post to retrieve.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>The matching <see cref="PostnomicPost"/>, or <see langword="null"/> when the API returns a 404.</returns>
    /// <exception cref="PostnomicApiException">The API rejected the request for a reason other than 404.</exception>
    Task<PostnomicPost?> GetPostAsync(string postId, CancellationToken cancellationToken = default);

    /// <summary>Publishes a post, making it visible to readers immediately.</summary>
    /// <param name="postId">The public ID of the post to publish.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>The updated <see cref="PostnomicPost"/>.</returns>
    /// <exception cref="PostnomicApiException">
    /// The API rejected the request — e.g. 409 when the post's status is not
    /// <see cref="PostnomicPostStatus.Draft"/>, <see cref="PostnomicPostStatus.Scheduled"/>, or
    /// <see cref="PostnomicPostStatus.InReview"/>, or when <see cref="PostnomicPost.ReviewRequired"/>
    /// is set but reviews are missing or not all approved.
    /// </exception>
    Task<PostnomicPost> PublishPostAsync(string postId, CancellationToken cancellationToken = default);

    /// <summary>Unpublishes a previously published post, taking it offline.</summary>
    /// <param name="postId">The public ID of the post to unpublish.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>The updated <see cref="PostnomicPost"/>.</returns>
    /// <exception cref="PostnomicApiException">
    /// The API rejected the request — e.g. 409 when the post's status is not
    /// <see cref="PostnomicPostStatus.Published"/>.
    /// </exception>
    Task<PostnomicPost> UnpublishPostAsync(string postId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives a post — a reversible soft-delete that unpublishes it (if live) and marks it
    /// <see cref="PostnomicPostStatus.Archived"/>. This is the API's recommended way to remove a
    /// post; it is what the Postnomic MCP server's own <c>delete_post</c> tool calls under the
    /// hood, in preference to the API's hard-delete <c>DELETE</c> endpoint (which this client
    /// does not expose).
    /// </summary>
    /// <param name="postId">The public ID of the post to archive.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>The updated <see cref="PostnomicPost"/>.</returns>
    /// <exception cref="PostnomicApiException">
    /// The API rejected the request — e.g. 404 when no such post exists on this blog.
    /// </exception>
    Task<PostnomicPost> ArchivePostAsync(string postId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a single image to the blog's media library and returns its stored location.
    /// </summary>
    /// <param name="content">The image bytes. The caller retains ownership and must dispose it.</param>
    /// <param name="fileName">
    /// The file name to store the image under (e.g. <c>"cover.jpg"</c>), including its extension —
    /// the API validates the extension against an allow-list of image/document/archive types.
    /// </param>
    /// <param name="contentType">The image's MIME type (e.g. <c>"image/jpeg"</c>).</param>
    /// <param name="path">
    /// An optional virtual folder path within the blog's media library (e.g. <c>"posts/2026-08"</c>).
    /// Pass <see langword="null"/> to upload to the root of the library.
    /// </param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>
    /// The stored <see cref="PostnomicMediaItem"/>, whose <see cref="PostnomicMediaItem.Url"/> can
    /// be passed directly as a post's cover/thumbnail/share image URL.
    /// </returns>
    /// <exception cref="PostnomicApiException">
    /// The API rejected the request — e.g. 400 for a disallowed file extension or content type,
    /// or 403 when the blog's storage quota is exhausted.
    /// </exception>
    Task<PostnomicMediaItem> UploadImageAsync(
        Stream content,
        string fileName,
        string contentType,
        string? path = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the post's translations — its content in languages other than the blog's default.
    /// The default-language content lives on the post itself (<see cref="PostnomicPost.Title"/>
    /// etc.), not in this list.
    /// </summary>
    /// <param name="postId">The public ID of the post whose translations to list.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>
    /// The post's translations, ordered by language. Empty when the post has none — this is the
    /// common case for a freshly created post, not an error.
    /// </returns>
    /// <exception cref="PostnomicApiException">
    /// The API rejected the request — e.g. 404 when no such post exists on this blog.
    /// </exception>
    Task<IReadOnlyList<PostnomicPostTranslation>> GetPostTranslationsAsync(
        string postId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates the post's translation in <paramref name="language"/>.
    /// </summary>
    /// <remarks>
    /// <b>The API rejects the blog's default language with 400.</b> That language is edited
    /// through the post itself (<see cref="UpdatePostAsync"/>), not as a translation — pass any
    /// other language code here.
    /// </remarks>
    /// <param name="postId">The public ID of the post to add or update a translation for.</param>
    /// <param name="language">
    /// The ISO-639-1 language code to create or update the translation in. Must not be the
    /// blog's default language.
    /// </param>
    /// <param name="request">The translated title, slug, and (optionally) content and excerpt.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>The created or updated <see cref="PostnomicPostTranslation"/>.</returns>
    /// <exception cref="PostnomicApiException">
    /// The API rejected the request — e.g. 400 when <paramref name="language"/> is the blog's
    /// default language, 404 when no such post exists on this blog, or 409 when another post
    /// already uses <see cref="PostnomicUpsertTranslationRequest.Slug"/> in this language on
    /// this blog.
    /// </exception>
    Task<PostnomicPostTranslation> SetPostTranslationAsync(
        string postId,
        string language,
        PostnomicUpsertTranslationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the post's translation in <paramref name="language"/>. The blog's default
    /// language cannot be deleted this way — it is part of the post itself.
    /// </summary>
    /// <param name="postId">The public ID of the post to remove a translation from.</param>
    /// <param name="language">The ISO-639-1 language code of the translation to delete.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <exception cref="PostnomicApiException">
    /// The API rejected the request — e.g. 400 when <paramref name="language"/> is the blog's
    /// default language, 404 when no such post exists on this blog, or 404 when the post has no
    /// translation in <paramref name="language"/>.
    /// </exception>
    Task DeletePostTranslationAsync(
        string postId,
        string language,
        CancellationToken cancellationToken = default);
}
