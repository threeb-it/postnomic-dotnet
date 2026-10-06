using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;

namespace Postnomic.Client;

/// <summary>
/// <see cref="HttpClient"/>-based implementation of <see cref="IPostnomicBlogService"/>
/// that calls the public Postnomic REST API on behalf of a single, pre-configured blog.
/// </summary>
/// <remarks>
/// This service is registered as a typed <see cref="HttpClient"/> via
/// <c>ServiceCollectionExtensions.AddPostnomicClient</c>. The <see cref="HttpClient"/>
/// base address is configured at DI registration time, and the <c>X-Api-Key</c> request header
/// is injected per-request by <see cref="PostnomicApiKeyHandler"/>.
/// </remarks>
public sealed class PostnomicBlogService(
    HttpClient httpClient,
    IOptions<PostnomicClientOptions> options) : IPostnomicBlogService
{
    private readonly PostnomicClientOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<PostnomicBlogInfo?> GetBlogAsync(CancellationToken cancellationToken = default)
    {
        return await GetJsonOrNullIfNotFoundAsync<PostnomicBlogInfo>(
            $"public/blogs/{_options.BlogSlug}", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<PostnomicTag>> GetTagsAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetJsonAsync<List<PostnomicTag>>(
            $"public/blogs/{_options.BlogSlug}/tags", cancellationToken);
        return result ?? [];
    }

    /// <inheritdoc />
    public async Task<List<PostnomicCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetJsonAsync<List<PostnomicCategory>>(
            $"public/blogs/{_options.BlogSlug}/categories", cancellationToken);
        return result ?? [];
    }

    /// <inheritdoc />
    public async Task<List<PostnomicAuthor>> GetAuthorsAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetJsonAsync<List<PostnomicAuthor>>(
            $"public/blogs/{_options.BlogSlug}/authors", cancellationToken);
        return result ?? [];
    }

    /// <inheritdoc />
    public async Task<PostnomicAuthorProfile?> GetAuthorProfileAsync(
        string authorSlug,
        CancellationToken cancellationToken = default)
    {
        var profile = await GetJsonOrNullIfNotFoundAsync<PostnomicAuthorProfile>(
            $"public/blogs/{_options.BlogSlug}/authors/{authorSlug}", cancellationToken);
        if (profile is null) return null;

        return profile with
        {
            ProfileImageUrl = ResolveImageUrl(profile.ProfileImageUrl),
            HeaderImageUrl = ResolveImageUrl(profile.HeaderImageUrl),
            RecentPosts = profile.RecentPosts
                .Select(p => p with { ThumbnailImageUrl = ResolveImageUrl(p.ThumbnailImageUrl) })
                .ToList()
        };
    }

    /// <inheritdoc />
    public async Task<PostnomicPagedResult<PostnomicPostSummary>> GetPostsAsync(
        int page = 1,
        int pageSize = 5,
        string? tag = null,
        string? category = null,
        string? author = null,
        string? search = null,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString()),
            ("tag", tag),
            ("category", category),
            ("author", author),
            ("search", search),
            ("lang", language));

        var result = await GetJsonAsync<PostnomicPagedResult<PostnomicPostSummary>>(
            $"public/blogs/{_options.BlogSlug}/posts{query}", cancellationToken);

        if (result is not null)
        {
            result = result with
            {
                Items = result.Items
                    .Select(p => p with { ThumbnailImageUrl = ResolveImageUrl(p.ThumbnailImageUrl) })
                    .ToList()
            };
        }

        return result ?? new PostnomicPagedResult<PostnomicPostSummary>
        {
            Items = [],
            Page = page,
            PageSize = pageSize,
            TotalCount = 0,
            TotalPages = 0
        };
    }

    /// <inheritdoc />
    public async Task<PostnomicPostDetail?> GetPostAsync(
        string postSlug,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        var langQuery = language is null ? string.Empty : $"?lang={Uri.EscapeDataString(language)}";
        var post = await GetJsonOrNullIfNotFoundAsync<PostnomicPostDetail>(
            $"public/blogs/{_options.BlogSlug}/posts/{postSlug}{langQuery}", cancellationToken);
        return post is null ? null : post with { CoverImageUrl = ResolveImageUrl(post.CoverImageUrl) };
    }

    /// <inheritdoc />
    public async Task<PostnomicComment?> CreateCommentAsync(
        string postSlug,
        PostnomicCreateCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        var path = $"public/blogs/{_options.BlogSlug}/posts/{postSlug}/comments";
        using var response = await httpClient.PostAsJsonAsync(path, request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await PostnomicResponseReader.ReadJsonAsync<PostnomicComment>(response, path, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<PostnomicPopularPost>> GetTopCommentedPostsAsync(
        int count = 3,
        CancellationToken cancellationToken = default)
    {
        var result = await GetJsonAsync<List<PostnomicPopularPost>>(
            $"public/blogs/{_options.BlogSlug}/posts/top-commented?count={count}",
            cancellationToken);
        return result ?? [];
    }

    /// <inheritdoc />
    public async Task<List<PostnomicPopularPost>> GetMostReadPostsAsync(
        int count = 3,
        CancellationToken cancellationToken = default)
    {
        var result = await GetJsonAsync<List<PostnomicPopularPost>>(
            $"public/blogs/{_options.BlogSlug}/posts/most-read?count={count}",
            cancellationToken);
        return result ?? [];
    }

    /// <inheritdoc />
    public async Task RecordPageViewAsync(
        string sessionId,
        string? postSlug = null,
        string? referrer = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            SessionId = sessionId,
            PostSlug = postSlug,
            Referrer = referrer
        };

        var response = await httpClient.PostAsJsonAsync(
            $"public/blogs/{_options.BlogSlug}/analytics/pageview",
            payload,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task UpdateReadDurationAsync(
        string sessionId,
        int durationSeconds,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            SessionId = sessionId,
            DurationSeconds = durationSeconds
        };

        var response = await httpClient.PatchAsJsonAsync(
            $"public/blogs/{_options.BlogSlug}/analytics/pageview",
            payload,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// GETs <paramref name="path"/> and deserializes the body. A non-success status throws
    /// <see cref="HttpRequestException"/> via <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>
    /// — exactly what <c>HttpClient.GetFromJsonAsync</c> did before — and an unusable success body
    /// throws <see cref="PostnomicUpstreamException"/> (see <see cref="PostnomicResponseReader"/>).
    /// </summary>
    private async Task<T?> GetJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await PostnomicResponseReader.ReadJsonAsync<T>(response, path, cancellationToken);
    }

    /// <summary>
    /// Like <see cref="GetJsonAsync{T}"/>, but a <c>404 Not Found</c> means "does not exist" and
    /// returns <see langword="null"/>.
    /// </summary>
    private async Task<T?> GetJsonOrNullIfNotFoundAsync<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        using var response = await httpClient.GetAsync(path, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await PostnomicResponseReader.ReadJsonAsync<T>(response, path, cancellationToken);
    }

    /// <summary>
    /// Resolves a relative image URL (e.g. <c>/media/blob/...</c>) to an absolute URL
    /// using the configured <see cref="PostnomicClientOptions.BaseUrl"/>.
    /// Returns <see langword="null"/> unchanged and leaves already-absolute URLs untouched.
    /// </summary>
    private string? ResolveImageUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return url;
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url;

        var baseUrl = _options.BaseUrl.TrimEnd('/');
        return $"{baseUrl}{(url.StartsWith('/') ? url : "/" + url)}";
    }

    private static string BuildQuery(params (string Key, string? Value)[] parameters)
    {
        var parts = parameters
            .Where(p => p.Value is not null)
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}");

        var qs = string.Join("&", parts);
        return string.IsNullOrEmpty(qs) ? string.Empty : "?" + qs;
    }
}
