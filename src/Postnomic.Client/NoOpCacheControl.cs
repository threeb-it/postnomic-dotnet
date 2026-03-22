using Postnomic.Client.Abstractions;

namespace Postnomic.Client;

/// <summary>
/// No-op implementation of <see cref="IPostnomicCacheControl"/> used when caching is disabled.
/// </summary>
internal sealed class NoOpCacheControl : IPostnomicCacheControl
{
    public void InvalidateAll() { }
    public void InvalidatePost(string postSlug) { }
    public void InvalidateMetadata() { }
}
