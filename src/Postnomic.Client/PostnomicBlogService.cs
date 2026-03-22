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
        var response = await httpClient.GetAsync($"public/blogs/{_options.BlogSlug}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PostnomicBlogInfo>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<PostnomicTag>> GetTagsAsync(CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<List<PostnomicTag>>(
            $"public/blogs/{_options.BlogSlug}/tags", cancellationToken);
        return result ?? [];
    }

    /// <inheritdoc />
    public async Task<List<PostnomicCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<List<PostnomicCategory>>(
            $"public/blogs/{_options.BlogSlug}/categories", cancellationToken);
        return result ?? [];
    }

    /// <inheritdoc />
    public async Task<List<PostnomicAuthor>> GetAuthorsAsync(CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<List<PostnomicAuthor>>(
            $"public/blogs/{_options.BlogSlug}/authors", cancellationToken);
        return result ?? [];
    }

    /// <inheritdoc />
    public async Task<PostnomicAuthorProfile?> GetAuthorProfileAsync(
        string authorSlug,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"public/blogs/{_options.BlogSlug}/authors/{authorSlug}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<PostnomicAuthorProfile>(cancellationToken);
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
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString()),
            ("tag", tag),
            ("category", category),
            ("author", author),
            ("search", search));

        var result = await httpClient.GetFromJsonAsync<PostnomicPagedResult<PostnomicPostSummary>>(
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
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"public/blogs/{_options.BlogSlug}/posts/{postSlug}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var post = await response.Content.ReadFromJsonAsync<PostnomicPostDetail>(cancellationToken);
        return post is null ? null : post with { CoverImageUrl = ResolveImageUrl(post.CoverImageUrl) };
    }

    /// <inheritdoc />
    public async Task<PostnomicComment?> CreateCommentAsync(
        string postSlug,
        PostnomicCreateCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"public/blogs/{_options.BlogSlug}/posts/{postSlug}/comments",
            request,
            cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<PostnomicComment>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<PostnomicPopularPost>> GetTopCommentedPostsAsync(
        int count = 3,
        CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<List<PostnomicPopularPost>>(
            $"public/blogs/{_options.BlogSlug}/posts/top-commented?count={count}",
            cancellationToken);
        return result ?? [];
    }

    /// <inheritdoc />
    public async Task<List<PostnomicPopularPost>> GetMostReadPostsAsync(
        int count = 3,
        CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<List<PostnomicPopularPost>>(
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
