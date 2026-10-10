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
/// HTTP status code and rejection reason. A success status whose body is empty or not valid JSON
/// surfaces as <see cref="PostnomicUpstreamException"/> instead — an upstream failure, after which a
/// write has most likely been applied (check before retrying it).
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
    /// <exception cref="PostnomicUpstreamException">
    /// The API answered with a success status but an empty or malformed (non-JSON) body. The write
    /// has most likely been applied; verify the state before retrying, or it may be duplicated.
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
    /// <exception cref="PostnomicUpstreamException">
    /// The API answered with a success status but an empty or malformed (non-JSON) body. The write
    /// has most likely been applied; verify the state before retrying, or it may be duplicated.
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
    /// <exception cref="PostnomicUpstreamException">
    /// The API answered with a success status but an empty or malformed (non-JSON) body. This is an
    /// upstream failure, not a missing resource.
    /// </exception>
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
    /// <exception cref="PostnomicUpstreamException">
    /// The API answered with a success status but an empty or malformed (non-JSON) body. The write
    /// has most likely been applied; verify the state before retrying, or it may be duplicated.
    /// </exception>
    Task<PostnomicPost> PublishPostAsync(string postId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single post by its slug, in any status by default — the authoring counterpart of
    /// <see cref="IPostnomicBlogService.GetPostAsync"/>, which can only see published posts. Use it
    /// to find out whether a post already exists before creating it.
    /// </summary>
    /// <param name="slug">The post's slug: its own, or that of one of its translations.</param>
    /// <param name="includeUnpublished">
    /// <see langword="true"/> (the default) also finds drafts and scheduled, unpublished and archived
    /// posts, and requires the <c>Editor</c> role. <see langword="false"/> looks among published posts
    /// only, which any member of the blog may do.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The post, or <see langword="null"/> if the blog has no such post.</returns>
    /// <exception cref="PostnomicApiException">Thrown when the API rejects the request (e.g. 403).</exception>
    Task<PostnomicPost?> GetPostBySlugAsync(
        string slug,
        bool includeUnpublished = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes a post together with its translations, reviews and cross-posts. This
    /// cannot be undone — <see cref="ArchivePostAsync"/> takes a post offline and keeps it.
    /// Requires the <c>Editor</c> role.
    /// </summary>
    /// <param name="postId">The post's public ID.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <exception cref="PostnomicApiException">Thrown when the API rejects the request (e.g. 404).</exception>
    Task DeletePostAsync(string postId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a blog as its members see it, including its
    /// <see cref="PostnomicBlog.DefaultLanguage"/>.
    /// </summary>
    /// <param name="blogId">
    /// The blog's public ID, or <see langword="null"/> for the configured
    /// <see cref="PostnomicClientOptions.BlogId"/>.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The blog, or <see langword="null"/> if it does not exist.</returns>
    /// <exception cref="PostnomicApiException">Thrown when the API rejects the request (e.g. 403 for a blog the token's owner is not a member of).</exception>
    Task<PostnomicBlog?> GetBlogAsync(string? blogId = null, CancellationToken cancellationToken = default);

    /// <summary>Lists every blog the token's owner is a member of.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <exception cref="PostnomicApiException">Thrown when the API rejects the request.</exception>
    Task<IReadOnlyList<PostnomicBlog>> GetBlogsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new blog owned by the token's owner, who becomes its <c>Admin</c>. This call is
    /// not scoped to <see cref="PostnomicClientOptions.BlogId"/>; to author in the new blog,
    /// configure a client with the returned <see cref="PostnomicBlog.PublicId"/>.
    /// </summary>
    /// <param name="request">The new blog.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <exception cref="PostnomicApiException">
    /// Thrown when the API rejects the request: 403 "Blog limit reached" when the owner's plan allows
    /// no further blog, 409 when the slug is taken or reserved.
    /// </exception>
    Task<PostnomicBlog> CreateBlogAsync(
        PostnomicCreateBlogRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a blog: its display name, its slug, or both. Requires the <c>Admin</c> role.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Changing the slug copies all of the blog's media to the new slug, rewrites the media
    /// references stored in that blog's posts and translations, and then switches the blog's
    /// address; no other blog is touched. The old slug and every old media URL keep answering for a
    /// grace period (<see cref="PostnomicBlogRenameResult.FormerSlugExpiresAt"/>, 90 days by
    /// default), so a reader client still configured with the old
    /// <see cref="PostnomicClientOptions.BlogSlug"/> keeps working until it is updated. The blog's
    /// public ID — <see cref="PostnomicClientOptions.BlogId"/> — does not change.
    /// </para>
    /// <para>
    /// A rename is reversed by renaming back. If the call fails part-way (for instance a 503 when
    /// not every media file arrived) the blog is unchanged; calling again with the same request
    /// continues where it stopped.
    /// </para>
    /// </remarks>
    /// <param name="request">The new name and/or slug.</param>
    /// <param name="blogId">
    /// The blog's public ID, or <see langword="null"/> for the configured
    /// <see cref="PostnomicClientOptions.BlogId"/>.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <exception cref="ArgumentException">Thrown when the request names neither a name nor a slug.</exception>
    /// <exception cref="PostnomicApiException">
    /// Thrown when the API rejects the request: 400 for an invalid slug, 403 below <c>Admin</c>,
    /// 409 when the slug is taken or reserved or a different rename is still in progress, 503 when
    /// the media copy could not be verified.
    /// </exception>
    Task<PostnomicBlogRenameResult> RenameBlogAsync(
        PostnomicRenameBlogRequest request,
        string? blogId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Unpublishes a previously published post, taking it offline.</summary>
    /// <param name="postId">The public ID of the post to unpublish.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>The updated <see cref="PostnomicPost"/>.</returns>
    /// <exception cref="PostnomicApiException">
    /// The API rejected the request — e.g. 409 when the post's status is not
    /// <see cref="PostnomicPostStatus.Published"/>.
    /// </exception>
    /// <exception cref="PostnomicUpstreamException">
    /// The API answered with a success status but an empty or malformed (non-JSON) body. The write
    /// has most likely been applied; verify the state before retrying, or it may be duplicated.
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
    /// <exception cref="PostnomicUpstreamException">
    /// The API answered with a success status but an empty or malformed (non-JSON) body. The write
    /// has most likely been applied; verify the state before retrying, or it may be duplicated.
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
    /// <exception cref="PostnomicUpstreamException">
    /// The API answered with a success status but an empty or malformed (non-JSON) body. The write
    /// has most likely been applied; verify the state before retrying, or it may be duplicated.
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
    /// <exception cref="PostnomicUpstreamException">
    /// The API answered with a success status but an empty or malformed (non-JSON) body. This is an
    /// upstream failure, not a missing resource.
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
    /// <exception cref="PostnomicUpstreamException">
    /// The API answered with a success status but an empty or malformed (non-JSON) body. The write
    /// has most likely been applied; verify the state before retrying, or it may be duplicated.
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
