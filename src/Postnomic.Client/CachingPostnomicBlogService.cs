using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Models;

namespace Postnomic.Client;

/// <summary>
/// Decorator that wraps an <see cref="IPostnomicBlogService"/> with in-memory caching.
/// Read operations are cached; write operations (comments, analytics) pass through and
/// invalidate relevant cache entries.
/// </summary>
internal sealed class CachingPostnomicBlogService : IPostnomicBlogService, IPostnomicCacheControl
{
    private readonly IPostnomicBlogService _inner;
    private readonly IMemoryCache _cache;
    private readonly PostnomicCacheOptions _cacheOptions;
    private readonly string _prefix;

    // Track all keys for InvalidateAll()
    private readonly HashSet<string> _trackedKeys = [];
    private readonly Lock _keysLock = new();

    public CachingPostnomicBlogService(
        IPostnomicBlogService inner,
        IMemoryCache cache,
        IOptions<PostnomicClientOptions> options)
    {
        _inner = inner;
        _cache = cache;
        _cacheOptions = options.Value.Cache ?? new PostnomicCacheOptions();
        _prefix = $"postnomic:{options.Value.BlogSlug}:";
    }

    private async Task<T?> GetOrTrackAsync<T>(string key, TimeSpan duration, Func<CancellationToken, Task<T?>> factory, CancellationToken ct)
    {
        if (_cache.TryGetValue(key, out T? cached))
            return cached;

        var value = await factory(ct);
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = duration
        };
        _cache.Set(key, value, options);
        TrackKey(key);
        return value;
    }

    private void TrackKey(string key)
    {
        lock (_keysLock)
        {
            _trackedKeys.Add(key);
        }
    }

    // ── Read operations (cached) ─────────────────────────────────────────

    public async Task<PostnomicBlogInfo?> GetBlogAsync(CancellationToken cancellationToken = default)
    {
        var key = $"{_prefix}blog";
        return await GetOrTrackAsync(key, _cacheOptions.MetadataDuration,
            ct => _inner.GetBlogAsync(ct), cancellationToken);
    }

    public async Task<List<PostnomicTag>> GetTagsAsync(CancellationToken cancellationToken = default)
    {
        var key = $"{_prefix}tags";
        return await GetOrTrackAsync<List<PostnomicTag>>(key, _cacheOptions.MetadataDuration,
            async ct => await _inner.GetTagsAsync(ct), cancellationToken) ?? [];
    }

    public async Task<List<PostnomicCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var key = $"{_prefix}categories";
        return await GetOrTrackAsync<List<PostnomicCategory>>(key, _cacheOptions.MetadataDuration,
            async ct => await _inner.GetCategoriesAsync(ct), cancellationToken) ?? [];
    }

    public async Task<List<PostnomicAuthor>> GetAuthorsAsync(CancellationToken cancellationToken = default)
    {
        var key = $"{_prefix}authors";
        return await GetOrTrackAsync<List<PostnomicAuthor>>(key, _cacheOptions.MetadataDuration,
            async ct => await _inner.GetAuthorsAsync(ct), cancellationToken) ?? [];
    }

    public async Task<PostnomicAuthorProfile?> GetAuthorProfileAsync(
        string authorSlug, CancellationToken cancellationToken = default)
    {
        var key = $"{_prefix}author:{authorSlug}";
        return await GetOrTrackAsync(key, _cacheOptions.MetadataDuration,
            ct => _inner.GetAuthorProfileAsync(authorSlug, ct), cancellationToken);
    }

    public async Task<PostnomicPagedResult<PostnomicPostSummary>> GetPostsAsync(
        int page = 1, int pageSize = 5,
        string? tag = null, string? category = null,
        string? author = null, string? search = null,
        CancellationToken cancellationToken = default)
    {
        var key = $"{_prefix}posts:p{page}s{pageSize}t{tag ?? "_"}c{category ?? "_"}a{author ?? "_"}q{search ?? "_"}";
        return await GetOrTrackAsync<PostnomicPagedResult<PostnomicPostSummary>>(key, _cacheOptions.PostListDuration,
            async ct => await _inner.GetPostsAsync(page, pageSize, tag, category, author, search, ct),
            cancellationToken) ?? new PostnomicPagedResult<PostnomicPostSummary>
        {
            Items = [],
            Page = page,
            PageSize = pageSize,
            TotalCount = 0,
            TotalPages = 0
        };
    }

    public async Task<PostnomicPostDetail?> GetPostAsync(
        string postSlug, CancellationToken cancellationToken = default)
    {
        var key = $"{_prefix}post:{postSlug}";
        return await GetOrTrackAsync(key, _cacheOptions.PostDetailDuration,
            ct => _inner.GetPostAsync(postSlug, ct), cancellationToken);
    }

    public async Task<List<PostnomicPopularPost>> GetTopCommentedPostsAsync(
        int count = 3, CancellationToken cancellationToken = default)
    {
        var key = $"{_prefix}top-commented:{count}";
        return await GetOrTrackAsync<List<PostnomicPopularPost>>(key, _cacheOptions.PopularPostsDuration,
            async ct => await _inner.GetTopCommentedPostsAsync(count, ct), cancellationToken) ?? [];
    }

    public async Task<List<PostnomicPopularPost>> GetMostReadPostsAsync(
        int count = 3, CancellationToken cancellationToken = default)
    {
        var key = $"{_prefix}most-read:{count}";
        return await GetOrTrackAsync<List<PostnomicPopularPost>>(key, _cacheOptions.PopularPostsDuration,
            async ct => await _inner.GetMostReadPostsAsync(count, ct), cancellationToken) ?? [];
    }

    // ── Write operations (pass-through with targeted invalidation) ───────

    public async Task<PostnomicComment?> CreateCommentAsync(
        string postSlug, PostnomicCreateCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.CreateCommentAsync(postSlug, request, cancellationToken);
        if (result is not null)
        {
            // Invalidate post detail (comment list changed)
            InvalidatePost(postSlug);
        }
        return result;
    }

    public Task RecordPageViewAsync(
        string sessionId, string? postSlug = null, string? referrer = null,
        CancellationToken cancellationToken = default)
        => _inner.RecordPageViewAsync(sessionId, postSlug, referrer, cancellationToken);

    public Task UpdateReadDurationAsync(
        string sessionId, int durationSeconds,
        CancellationToken cancellationToken = default)
        => _inner.UpdateReadDurationAsync(sessionId, durationSeconds, cancellationToken);

    // ── IPostnomicCacheControl ───────────────────────────────────────────

    public void InvalidateAll()
    {
        lock (_keysLock)
        {
            foreach (var key in _trackedKeys)
                _cache.Remove(key);
            _trackedKeys.Clear();
        }
    }

    public void InvalidatePost(string postSlug)
    {
        _cache.Remove($"{_prefix}post:{postSlug}");
    }

    public void InvalidateMetadata()
    {
        var metadataKeys = new[] { "blog", "tags", "categories", "authors" };
        foreach (var suffix in metadataKeys)
            _cache.Remove($"{_prefix}{suffix}");

        // Also remove all author profile keys
        lock (_keysLock)
        {
            var authorKeys = _trackedKeys.Where(k => k.StartsWith($"{_prefix}author:")).ToList();
            foreach (var key in authorKeys)
            {
                _cache.Remove(key);
                _trackedKeys.Remove(key);
            }
        }
    }
}
