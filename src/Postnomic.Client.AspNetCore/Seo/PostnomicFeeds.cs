using Microsoft.AspNetCore.Http;
using Postnomic.Client.Abstractions;
using Postnomic.Client.Abstractions.Seo;

namespace Postnomic.Client.AspNetCore.Seo;

/// <summary>
/// Builds a sitemap (<c>sitemap.xml</c>) and an RSS 2.0 feed (<c>rss.xml</c>) for a single
/// registered Postnomic blog, driven by <see cref="IPostnomicBlogService.GetPostsAsync"/>. Used
/// by <c>MapPostnomicBlog</c> to serve both documents per registered blog.
/// </summary>
/// <remarks>
/// This is a thin <see cref="HttpRequest"/>-aware adapter over
/// <see cref="Postnomic.Client.Abstractions.Seo.PostnomicFeedBuilder"/>, the framework-free core
/// shared with non-ASP.NET Core hosts (e.g. Blazor). It derives the absolute base URL from the
/// current request's scheme + host — the same derivation <see cref="PostnomicSeo"/> uses — and
/// delegates the actual XML construction, so output is byte-identical to calling
/// <see cref="PostnomicFeedBuilder"/> directly with that base URL.
/// </remarks>
public static class PostnomicFeeds
{
    /// <summary>
    /// Builds a sitemap <c>urlset</c> XML document listing the blog index page and every post,
    /// via <see cref="PostnomicFeedBuilder.BuildSitemapAsync"/>.
    /// </summary>
    public static Task<string> BuildSitemapAsync(
        IPostnomicBlogService blogService,
        HttpRequest request,
        string basePath,
        PostnomicLanguageRouteStyle style,
        CancellationToken cancellationToken = default)
        => PostnomicFeedBuilder.BuildSitemapAsync(blogService, BaseUri(request), basePath, style, cancellationToken);

    /// <summary>
    /// Builds an RSS 2.0 <c>&lt;rss&gt;&lt;channel&gt;</c> XML document for the blog's most
    /// recent posts, via <see cref="PostnomicFeedBuilder.BuildRssAsync"/>.
    /// </summary>
    public static Task<string> BuildRssAsync(
        IPostnomicBlogService blogService,
        HttpRequest request,
        string basePath,
        PostnomicLanguageRouteStyle style,
        string channelTitle,
        string? channelDescription,
        CancellationToken cancellationToken = default)
        => PostnomicFeedBuilder.BuildRssAsync(
            blogService, BaseUri(request), basePath, style, channelTitle, channelDescription, cancellationToken);

    private static string BaseUri(HttpRequest request) => $"{request.Scheme}://{request.Host}";
}
