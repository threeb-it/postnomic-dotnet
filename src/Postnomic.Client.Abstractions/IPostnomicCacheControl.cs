namespace Postnomic.Client.Abstractions;

/// <summary>
/// Allows explicit invalidation of cached data in the Postnomic client SDK.
/// Resolve this interface from DI to clear stale entries programmatically.
/// When caching is disabled, this resolves to a no-op implementation.
/// </summary>
public interface IPostnomicCacheControl
{
    /// <summary>Removes all cached entries.</summary>
    void InvalidateAll();

    /// <summary>Removes the cached detail for a specific post.</summary>
    void InvalidatePost(string postSlug);

    /// <summary>Removes all cached metadata (blog info, tags, categories, authors).</summary>
    void InvalidateMetadata();
}
