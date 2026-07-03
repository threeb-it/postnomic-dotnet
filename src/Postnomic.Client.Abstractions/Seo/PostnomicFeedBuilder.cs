using System.Globalization;
using System.Xml.Linq;
using Postnomic.Client.Abstractions.Models;

namespace Postnomic.Client.Abstractions.Seo;

/// <summary>
/// Builds a sitemap (<c>sitemap.xml</c>) and an RSS 2.0 feed (<c>rss.xml</c>) for a single
/// registered Postnomic blog, driven by <see cref="IPostnomicBlogService.GetPostsAsync"/>. This is
/// the framework-free core: it takes an already-resolved <c>absoluteBaseUrl</c> instead of an
/// ASP.NET Core <c>HttpRequest</c>, so it can be called from any hosting model (Blazor included)
/// that has no ambient HTTP request to derive scheme+host from.
/// <c>Postnomic.Client.AspNetCore.Seo.PostnomicFeeds</c> computes the base URL from the current
/// request and delegates here, so both hosting models emit byte-identical XML.
/// </summary>
public static class PostnomicFeedBuilder
{
    // The Postnomic API enforces a hard maximum of 50 items per page (see
    // IPostnomicBlogService.GetPostsAsync), so this is the largest page we can request.
    private const int SitemapPageSize = 50;

    // Safety cap on how many pages BuildSitemapAsync will fetch from the API for a single
    // sitemap request. 40 pages * 50 posts/page = 2,000 posts, comfortably covering a single
    // sitemap file (the sitemap protocol allows up to 50,000 <url> entries per file) while
    // bounding worst-case request fan-out for blogs with an unexpectedly large post count. A
    // future task can add sitemap index (sitemap-N.xml) support if a blog ever exceeds this.
    private const int SitemapMaxPages = 40;

    // RSS feeds conventionally surface only the most recent items, not the full catalog.
    private const int RssRecentPostCount = 20;

    private static readonly XNamespace SitemapNs = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private static readonly XNamespace XhtmlNs = "http://www.w3.org/1999/xhtml";

    /// <summary>
    /// Builds a sitemap <c>urlset</c> XML document listing the blog index page and every post
    /// (fetched via <see cref="IPostnomicBlogService.GetPostsAsync"/>, up to
    /// <see cref="SitemapMaxPages"/> pages), with an <c>xhtml:link</c> hreflang alternate for
    /// each language the post is available in.
    /// </summary>
    public static async Task<string> BuildSitemapAsync(
        IPostnomicBlogService blogService,
        string absoluteBaseUrl,
        string basePath,
        PostnomicLanguageRouteStyle style,
        CancellationToken cancellationToken = default)
    {
        var posts = await FetchAllPostsForSitemapAsync(blogService, cancellationToken).ConfigureAwait(false);
        return BuildSitemapXml(posts, absoluteBaseUrl, basePath, style);
    }

    /// <summary>
    /// Builds an RSS 2.0 <c>&lt;rss&gt;&lt;channel&gt;</c> XML document for the blog's most
    /// recent posts (up to <see cref="RssRecentPostCount"/>) in the blog's default language.
    /// </summary>
    public static async Task<string> BuildRssAsync(
        IPostnomicBlogService blogService,
        string absoluteBaseUrl,
        string basePath,
        PostnomicLanguageRouteStyle style,
        string channelTitle,
        string? channelDescription,
        CancellationToken cancellationToken = default)
    {
        var page = await blogService
            .GetPostsAsync(page: 1, pageSize: RssRecentPostCount, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return BuildRssXml(page.Items, absoluteBaseUrl, basePath, style, channelTitle, channelDescription);
    }

    /// <summary>Builds the sitemap XML string from an already-fetched list of posts.</summary>
    internal static string BuildSitemapXml(
        ICollection<PostnomicPostSummary> posts,
        string absoluteBaseUrl,
        string basePath,
        PostnomicLanguageRouteStyle style)
    {
        var urlset = new XElement(SitemapNs + "urlset", new XAttribute(XNamespace.Xmlns + "xhtml", XhtmlNs));

        // Blog index, so crawlers discover the listing page even when no post links back to it.
        urlset.Add(BuildIndexUrlElement(absoluteBaseUrl, basePath, style, DetermineDefaultLanguage(posts)));

        foreach (var post in posts)
            urlset.Add(BuildPostUrlElement(post, absoluteBaseUrl, basePath, style));

        return ToXmlString(urlset);
    }

    /// <summary>Builds the RSS XML string from an already-fetched list of posts.</summary>
    internal static string BuildRssXml(
        ICollection<PostnomicPostSummary> posts,
        string absoluteBaseUrl,
        string basePath,
        PostnomicLanguageRouteStyle style,
        string channelTitle,
        string? channelDescription)
    {
        var channelLink = ToAbsoluteUrl(
            absoluteBaseUrl,
            PostnomicRouteBuilder.BuildIndex(basePath, style, ResolveIndexLanguage(style, DetermineDefaultLanguage(posts))));

        var channel = new XElement("channel",
            new XElement("title", channelTitle),
            new XElement("link", channelLink),
            new XElement("description", channelDescription ?? channelTitle));

        foreach (var post in posts)
        {
            var link = ToAbsoluteUrl(absoluteBaseUrl, PostnomicRouteBuilder.BuildPost(basePath, style, post.Language, post.Slug));

            channel.Add(new XElement("item",
                new XElement("title", post.Title),
                new XElement("link", link),
                new XElement("description", post.Excerpt ?? ""),
                new XElement("pubDate", ToRfc822(post.PublishedAt)),
                new XElement("guid", link)));
        }

        var rss = new XElement("rss", new XAttribute("version", "2.0"), channel);
        return ToXmlString(rss);
    }

    private static XElement BuildIndexUrlElement(
        string absoluteBaseUrl, string basePath, PostnomicLanguageRouteStyle style, string? defaultLanguage)
    {
        var loc = ToAbsoluteUrl(absoluteBaseUrl, PostnomicRouteBuilder.BuildIndex(basePath, style, ResolveIndexLanguage(style, defaultLanguage)));
        return new XElement(SitemapNs + "url", new XElement(SitemapNs + "loc", loc));
    }

    /// <summary>
    /// Resolves the <c>lang</c> argument to pass to <see cref="PostnomicRouteBuilder.BuildIndex"/>
    /// for the blog index URL advertised in the sitemap <c>&lt;loc&gt;</c> and RSS channel
    /// <c>&lt;link&gt;</c>. Under <see cref="PostnomicLanguageRouteStyle.Prefix"/> there is no bare
    /// <c>{basePath}</c> route (only <c>{basePath}/{lang}</c> is registered — see
    /// <c>PostnomicBlogAreaRouteConvention</c>), so passing <see langword="null"/> there like the
    /// other styles do would advertise a URL that 404s. Passing <paramref name="defaultLanguage"/>
    /// instead yields the real <c>/{defaultLanguage}/{basePath}</c> route. Suffix/None keep
    /// <see langword="null"/> (lang: null) since a bare <c>{basePath}</c> route IS valid there.
    /// </summary>
    private static string? ResolveIndexLanguage(PostnomicLanguageRouteStyle style, string? defaultLanguage)
        => style == PostnomicLanguageRouteStyle.Prefix ? defaultLanguage : null;

    /// <summary>
    /// Determines the blog's "default" language from an already-fetched batch of posts, for use
    /// when building the Prefix-style index URL (see <see cref="ResolveIndexLanguage(PostnomicLanguageRouteStyle, string?)"/>).
    /// The SDK has no dedicated "blog default language" field, so this approximates it as the most
    /// common <see cref="PostnomicPostSummary.Language"/> across the batch (ties broken by
    /// whichever language appears first), which is more robust against a single mistranslated or
    /// out-of-order post than always trusting <c>posts[0]</c>. Returns <see langword="null"/> when
    /// <paramref name="posts"/> is empty (an empty blog has no language to infer; the Prefix index
    /// URL falls back to a bare route in that edge case, same as before this fix).
    /// </summary>
    private static string? DetermineDefaultLanguage(ICollection<PostnomicPostSummary> posts) =>
        posts
            .GroupBy(p => p.Language, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

    private static XElement BuildPostUrlElement(
        PostnomicPostSummary post, string absoluteBaseUrl, string basePath, PostnomicLanguageRouteStyle style)
    {
        var loc = ToAbsoluteUrl(absoluteBaseUrl, PostnomicRouteBuilder.BuildPost(basePath, style, post.Language, post.Slug));

        var url = new XElement(SitemapNs + "url",
            new XElement(SitemapNs + "loc", loc),
            new XElement(SitemapNs + "lastmod",
                PostnomicSeoBuilder.NormalizeToUtc(post.PublishedAt).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));

        foreach (var lang in post.AvailableLanguages)
        {
            var altUrl = ToAbsoluteUrl(absoluteBaseUrl, PostnomicRouteBuilder.BuildPost(basePath, style, lang, post.Slug));
            url.Add(new XElement(XhtmlNs + "link",
                new XAttribute("rel", "alternate"),
                new XAttribute("hreflang", lang),
                new XAttribute("href", altUrl)));
        }

        return url;
    }

    private static async Task<List<PostnomicPostSummary>> FetchAllPostsForSitemapAsync(
        IPostnomicBlogService blogService, CancellationToken cancellationToken)
    {
        var all = new List<PostnomicPostSummary>();

        for (var page = 1; page <= SitemapMaxPages; page++)
        {
            var result = await blogService
                .GetPostsAsync(page: page, pageSize: SitemapPageSize, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (result.Items.Count == 0)
                break;

            all.AddRange(result.Items);

            if (page >= result.TotalPages)
                break;
        }

        return all;
    }

    private static string ToAbsoluteUrl(string absoluteBaseUrl, string pathOrUrl) =>
        PostnomicSeoBuilder.ToAbsoluteUrl(absoluteBaseUrl, pathOrUrl);

    private static string ToRfc822(DateTime dateTime) =>
        PostnomicSeoBuilder.NormalizeToUtc(dateTime).ToString("r", CultureInfo.InvariantCulture);

    private static string ToXmlString(XElement root)
    {
        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
        using var writer = new StringWriter();
        doc.Save(writer, SaveOptions.DisableFormatting);
        return writer.ToString();
    }
}
