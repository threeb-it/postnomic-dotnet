using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;

namespace Postnomic.Client;

/// <summary>
/// <see cref="HttpClient"/>-based implementation of <see cref="IPostnomicAuthoringService"/>
/// that calls the Personal-Access-Token-authenticated Postnomic management API on behalf of a
/// single, pre-configured blog.
/// </summary>
/// <remarks>
/// This service is registered as a typed <see cref="HttpClient"/> via
/// <c>ServiceCollectionExtensions.AddPostnomicAuthoringClient</c>. The <see cref="HttpClient"/>
/// base address is configured at DI registration time, and the
/// <c>Authorization: Bearer</c> header is injected per-request by
/// <see cref="PostnomicPersonalAccessTokenHandler"/>.
/// </remarks>
public sealed class PostnomicAuthoringService(
    HttpClient httpClient,
    IOptions<PostnomicClientOptions> options) : IPostnomicAuthoringService
{
    private readonly PostnomicClientOptions _options = options.Value;

    private string PostsRoute => $"blogs/{_options.BlogId}/posts";

    private string TranslationsRoute(string postId) => $"{PostsRoute}/{postId}/translations";

    /// <inheritdoc />
    public async Task<PostnomicPost> CreatePostAsync(
        PostnomicCreatePostRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(PostsRoute, request, cancellationToken);
        var post = await ReadOrThrowAsync<PostnomicPost>(response, PostsRoute, cancellationToken);

        // With PublishedAt the API created the post already published; a second publish would be
        // refused, and would overwrite the date if it were not.
        if (request.PublishImmediately && request.PublishedAt is null)
        {
            post = await PublishPostAsync(post.PublicId, cancellationToken);
        }

        return post;
    }

    /// <inheritdoc />
    public async Task<PostnomicPost> UpdatePostAsync(
        string postId,
        PostnomicUpdatePostRequest request,
        CancellationToken cancellationToken = default)
    {
        var path = $"{PostsRoute}/{postId}";
        using var response = await httpClient.PutAsJsonAsync(path, request, cancellationToken);
        return await ReadOrThrowAsync<PostnomicPost>(response, path, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PostnomicPost?> GetPostAsync(string postId, CancellationToken cancellationToken = default)
    {
        var path = $"{PostsRoute}/{postId}";
        using var response = await httpClient.GetAsync(path, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        return await ReadOrThrowAsync<PostnomicPost>(response, path, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PostnomicPost?> GetPostBySlugAsync(
        string slug,
        bool includeUnpublished = true,
        CancellationToken cancellationToken = default)
    {
        var path = $"{PostsRoute}/by-slug/{Uri.EscapeDataString(slug)}?includeUnpublished={(includeUnpublished ? "true" : "false")}";
        using var response = await httpClient.GetAsync(path, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        return await ReadOrThrowAsync<PostnomicPost>(response, path, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeletePostAsync(string postId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"{PostsRoute}/{postId}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PostnomicBlog?> GetBlogAsync(string? blogId = null, CancellationToken cancellationToken = default)
    {
        var path = $"blogs/{blogId ?? _options.BlogId}";
        using var response = await httpClient.GetAsync(path, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        return await ReadOrThrowAsync<PostnomicBlog>(response, path, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PostnomicBlog>> GetBlogsAsync(CancellationToken cancellationToken = default)
    {
        const string path = "blogs";
        using var response = await httpClient.GetAsync(path, cancellationToken);
        return await ReadOrThrowAsync<List<PostnomicBlog>>(response, path, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PostnomicBlog> CreateBlogAsync(
        PostnomicCreateBlogRequest request,
        CancellationToken cancellationToken = default)
    {
        const string path = "blogs";
        using var response = await httpClient.PostAsJsonAsync(path, request, cancellationToken);
        return await ReadOrThrowAsync<PostnomicBlog>(response, path, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PostnomicBlogRenameResult> RenameBlogAsync(
        PostnomicRenameBlogRequest request,
        string? blogId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) && string.IsNullOrWhiteSpace(request.Slug))
            throw new ArgumentException("Supply a new name, a new slug, or both.", nameof(request));

        var path = $"blogs/{blogId ?? _options.BlogId}/rename";
        using var response = await httpClient.PostAsJsonAsync(path, request, cancellationToken);
        return await ReadOrThrowAsync<PostnomicBlogRenameResult>(response, path, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PostnomicPost> PublishPostAsync(string postId, CancellationToken cancellationToken = default)
    {
        var path = $"{PostsRoute}/{postId}/publish";
        using var response = await httpClient.PostAsync(path, content: null, cancellationToken);
        return await ReadOrThrowAsync<PostnomicPost>(response, path, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PostnomicPost> UnpublishPostAsync(string postId, CancellationToken cancellationToken = default)
    {
        var path = $"{PostsRoute}/{postId}/unpublish";
        using var response = await httpClient.PostAsync(path, content: null, cancellationToken);
        return await ReadOrThrowAsync<PostnomicPost>(response, path, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PostnomicPost> ArchivePostAsync(string postId, CancellationToken cancellationToken = default)
    {
        var path = $"{PostsRoute}/{postId}/archive";
        using var response = await httpClient.PostAsync(path, content: null, cancellationToken);
        return await ReadOrThrowAsync<PostnomicPost>(response, path, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PostnomicMediaItem> UploadImageAsync(
        Stream content,
        string fileName,
        string contentType,
        string? path = null,
        CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "files", fileName);

        var query = string.IsNullOrEmpty(path) ? "" : $"?path={Uri.EscapeDataString(path)}";
        var uploadRoute = $"blogs/{_options.BlogId}/media/upload{query}";
        using var response = await httpClient.PostAsync(uploadRoute, form, cancellationToken);

        var items = await ReadOrThrowAsync<List<PostnomicMediaItem>>(response, uploadRoute, cancellationToken);
        if (items.Count == 0)
        {
            throw new PostnomicApiException(response.StatusCode, "The API accepted the upload but returned no media items.");
        }

        return items[0];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PostnomicPostTranslation>> GetPostTranslationsAsync(
        string postId,
        CancellationToken cancellationToken = default)
    {
        var path = TranslationsRoute(postId);
        using var response = await httpClient.GetAsync(path, cancellationToken);
        return await ReadOrThrowAsync<List<PostnomicPostTranslation>>(response, path, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PostnomicPostTranslation> SetPostTranslationAsync(
        string postId,
        string language,
        PostnomicUpsertTranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        var path = $"{TranslationsRoute(postId)}/{Uri.EscapeDataString(language)}";
        using var response = await httpClient.PutAsJsonAsync(path, request, cancellationToken);
        return await ReadOrThrowAsync<PostnomicPostTranslation>(response, path, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeletePostTranslationAsync(
        string postId,
        string language,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync(
            $"{TranslationsRoute(postId)}/{Uri.EscapeDataString(language)}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <summary>
    /// Throws <see cref="PostnomicApiException"/> carrying the status code and the API's
    /// rejection reason (its response body, when present, otherwise the HTTP reason phrase) when
    /// <paramref name="response"/> is not a success. The single error-mapping path shared by
    /// <see cref="ReadOrThrowAsync{T}"/> and calls with no response body to deserialize
    /// (<see cref="DeletePostTranslationAsync"/>).
    /// </summary>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body;
        throw new PostnomicApiException(response.StatusCode, message);
    }

    /// <summary>
    /// Reads and deserializes a successful response body, or throws
    /// <see cref="PostnomicApiException"/> via <see cref="EnsureSuccessAsync"/>. A success status
    /// with an empty or malformed body throws <see cref="PostnomicUpstreamException"/> (see
    /// <see cref="PostnomicResponseReader"/>); a body that is the JSON literal <c>null</c> still
    /// throws <see cref="PostnomicApiException"/>, as before.
    /// </summary>
    private static async Task<T> ReadOrThrowAsync<T>(
        HttpResponseMessage response,
        string path,
        CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);

        var result = await PostnomicResponseReader.ReadJsonAsync<T>(response, path, cancellationToken);
        return result ?? throw new PostnomicApiException(response.StatusCode, "The API returned an empty response body.");
    }
}
