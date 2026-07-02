using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Postnomic.Client.Abstractions;
using Postnomic.Client.AspNetCore.Areas.Blog.Pages;

namespace Postnomic.Client.AspNetCore.Seo;

/// <summary>
/// The data needed by <c>_SeoHead.cshtml</c> to render canonical/OpenGraph/Twitter/hreflang tags
/// and a JSON-LD structured-data script for a single blog page.
/// All URLs (<see cref="CanonicalUrl"/>, <see cref="ImageUrl"/>, and the URLs inside
/// <see cref="Alternates"/>) are absolute (scheme + host + path).
/// </summary>
public sealed record PostnomicSeoModel
{
    /// <summary>The page title, used for <c>og:title</c> / <c>twitter:title</c>.</summary>
    public required string Title { get; init; }

    /// <summary>The meta description. <see langword="null"/> when none is available.</summary>
    public string? Description { get; init; }

    /// <summary>The absolute canonical URL of the page.</summary>
    public required string CanonicalUrl { get; init; }

    /// <summary>The absolute URL of the representative image for the page, if any.</summary>
    public string? ImageUrl { get; init; }

    /// <summary>The OpenGraph object type (e.g. <c>"website"</c>, <c>"article"</c>, <c>"profile"</c>).</summary>
    public string OgType { get; init; } = "website";

    /// <summary>The blog's display name, used for <c>og:site_name</c>.</summary>
    public string SiteName { get; init; } = "";

    /// <summary>The value of the <c>robots</c> meta tag. Defaults to <c>"index, follow"</c>.</summary>
    public string Robots { get; init; } = "index, follow";

    /// <summary>
    /// The OpenGraph locale in <c>xx_XX</c> form (e.g. <c>"en_US"</c>, <c>"de_DE"</c>), used for
    /// <c>og:locale</c>.
    /// </summary>
    public string Locale { get; init; } = "en_US";

    /// <summary>
    /// hreflang alternates for this page: one (language code, absolute URL) pair per available
    /// language. Never <see langword="null"/>; may be empty.
    /// </summary>
    public IReadOnlyList<(string Lang, string Url)> Alternates { get; init; } = [];

    /// <summary>
    /// A JSON-LD document (already serialized, HTML-safe) to embed in an
    /// <c>application/ld+json</c> script tag. <see langword="null"/> when no structured data
    /// applies.
    /// </summary>
    public string? JsonLd { get; init; }

    // ── Article-only extras (rendered only when OgType == "article") ─────────

    /// <summary>The post's publish date, for <c>article:published_time</c>. Post pages only.</summary>
    public DateTime? PublishedAt { get; init; }

    /// <summary>The post's author display name, for <c>article:author</c>. Post pages only.</summary>
    public string? AuthorName { get; init; }

    /// <summary>The post's tag names, for repeated <c>article:tag</c> meta tags. Post pages only.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}

/// <summary>
/// Builds a <see cref="PostnomicSeoModel"/> (canonical/OpenGraph/Twitter/hreflang/JSON-LD data)
/// for each of the three Blog area pages (Index, Post, Author).
/// </summary>
public static class PostnomicSeo
{
    private const string SchemaContext = "https://schema.org";

    /// <summary>Builds the SEO model for the blog index (listing) page.</summary>
    public static PostnomicSeoModel ForIndex(HttpRequest request, IndexModel model)
    {
        var canonical = ToAbsoluteUrl(request,
            PostnomicRouteBuilder.BuildIndex(model.BasePath, model.RouteStyle, model.Lang));
        var title = model.BlogInfo?.Name ?? "Blog";
        var description = model.BlogInfo?.Description;
        var lang = model.Lang ?? "en";

        var blogNode = new JsonObject
        {
            ["@type"] = "Blog",
            ["name"] = title,
            ["url"] = canonical,
        };
        if (!string.IsNullOrWhiteSpace(description))
            blogNode["description"] = description;

        var itemListElements = new JsonArray();
        var position = 1;
        foreach (var post in model.Posts.Items)
        {
            itemListElements.Add(new JsonObject
            {
                ["@type"] = "ListItem",
                ["position"] = position++,
                ["url"] = ToAbsoluteUrl(request,
                    PostnomicRouteBuilder.BuildPost(model.BasePath, model.RouteStyle, model.Lang, post.Slug)),
                ["name"] = post.Title,
            });
        }
        var itemList = new JsonObject
        {
            ["@type"] = "ItemList",
            ["itemListElement"] = itemListElements,
        };

        var breadcrumb = BuildBreadcrumb((title, canonical));

        return new PostnomicSeoModel
        {
            Title = title,
            Description = description,
            CanonicalUrl = canonical,
            OgType = "website",
            SiteName = title,
            Locale = ToOgLocale(lang),
            Alternates = [(lang, canonical)],
            JsonLd = SerializeGraph(blogNode, itemList, breadcrumb),
        };
    }

    /// <summary>Builds the SEO model for a single blog post page.</summary>
    public static PostnomicSeoModel ForPost(HttpRequest request, PostModel model)
    {
        var post = model.Post;
        // Self-referential canonical: canonicalize to the URL of the language variant actually
        // being rendered (model.Lang), not model.CanonicalUrl (which always points at the
        // default-language URL and is kept that way for existing consumers — see README).
        var canonical = ToAbsoluteUrl(request,
            PostnomicRouteBuilder.BuildPost(model.BasePath, model.RouteStyle, model.Lang, model.PostSlug));
        var image = ToAbsoluteUrlOrNull(request, post.CoverImageUrl);
        var alternates = model.AlternateLanguageUrls
            .Select(a => (a.Language, ToAbsoluteUrl(request, a.Url)))
            .ToList();

        var description = BuildDescription(post.Excerpt, post.Content, post.Title);

        var author = new JsonObject
        {
            ["@type"] = "Person",
            ["name"] = post.AuthorName,
        };

        var blogPosting = new JsonObject
        {
            ["@type"] = "BlogPosting",
            ["headline"] = post.Title,
            ["description"] = description,
            ["datePublished"] = post.PublishedAt.ToString("O"),
            ["inLanguage"] = post.Language,
            ["author"] = author,
            ["mainEntityOfPage"] = new JsonObject
            {
                ["@type"] = "WebPage",
                ["@id"] = canonical,
            },
        };
        if (!string.IsNullOrEmpty(image))
            blogPosting["image"] = image;

        var indexUrl = ToAbsoluteUrl(request,
            PostnomicRouteBuilder.BuildIndex(model.BasePath, model.RouteStyle, model.Lang));
        var blogName = model.BlogInfo?.Name ?? "Blog";
        var breadcrumb = BuildBreadcrumb((blogName, indexUrl), (post.Title, canonical));

        return new PostnomicSeoModel
        {
            Title = post.Title,
            Description = description,
            CanonicalUrl = canonical,
            ImageUrl = string.IsNullOrEmpty(image) ? null : image,
            OgType = "article",
            SiteName = blogName,
            Locale = ToOgLocale(post.Language),
            Alternates = alternates,
            JsonLd = SerializeGraph(blogPosting, breadcrumb),
            PublishedAt = post.PublishedAt,
            AuthorName = post.AuthorName,
            Tags = post.Tags.Select(t => t.Name).ToList(),
        };
    }

    /// <summary>Builds the SEO model for an author profile page.</summary>
    public static PostnomicSeoModel ForAuthor(HttpRequest request, AuthorModel model)
    {
        var profile = model.Profile;
        var canonical = ToAbsoluteUrl(request,
            PostnomicRouteBuilder.BuildAuthor(model.BasePath, model.RouteStyle, model.Lang, model.AuthorSlug));
        var image = ToAbsoluteUrlOrNull(request, profile.ProfileImageUrl);
        var lang = model.Lang ?? "en";

        var description = !string.IsNullOrWhiteSpace(profile.Headline)
            ? profile.Headline
            : !string.IsNullOrWhiteSpace(profile.Bio)
                ? Truncate(StripHtml(profile.Bio), 200)
                : null;

        var person = new JsonObject
        {
            ["@type"] = "Person",
            ["name"] = profile.Name,
        };
        if (!string.IsNullOrWhiteSpace(profile.JobTitle))
            person["jobTitle"] = profile.JobTitle;
        if (!string.IsNullOrWhiteSpace(profile.Company))
            person["worksFor"] = new JsonObject { ["@type"] = "Organization", ["name"] = profile.Company };
        if (!string.IsNullOrWhiteSpace(profile.Headline))
            person["description"] = profile.Headline;
        if (!string.IsNullOrEmpty(image))
            person["image"] = image;
        if (profile.SocialLinks.Count > 0)
        {
            var sameAs = new JsonArray();
            foreach (var link in profile.SocialLinks)
                sameAs.Add(link.Url);
            person["sameAs"] = sameAs;
        }

        var profilePage = new JsonObject
        {
            ["@type"] = "ProfilePage",
            ["mainEntity"] = person,
        };

        var indexUrl = ToAbsoluteUrl(request,
            PostnomicRouteBuilder.BuildIndex(model.BasePath, model.RouteStyle, model.Lang));
        var breadcrumb = BuildBreadcrumb(("Blog", indexUrl), (profile.Name, canonical));

        return new PostnomicSeoModel
        {
            Title = profile.Name,
            Description = description,
            CanonicalUrl = canonical,
            ImageUrl = string.IsNullOrEmpty(image) ? null : image,
            OgType = "profile",
            SiteName = profile.Name,
            Locale = ToOgLocale(lang),
            Alternates = [(lang, canonical)],
            JsonLd = SerializeGraph(profilePage, breadcrumb),
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a path-relative or already-absolute, non-empty URL to an absolute URL using
    /// <paramref name="request"/>'s scheme and host.
    /// </summary>
    public static string ToAbsoluteUrl(HttpRequest request, string pathOrUrl)
    {
        if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out _))
            return pathOrUrl;

        var baseUrl = $"{request.Scheme}://{request.Host}";
        return pathOrUrl.StartsWith('/') ? $"{baseUrl}{pathOrUrl}" : $"{baseUrl}/{pathOrUrl}";
    }

    /// <summary>
    /// Same as <see cref="ToAbsoluteUrl(HttpRequest, string)"/> but returns
    /// <see langword="null"/> unchanged when <paramref name="pathOrUrl"/> is null or empty.
    /// </summary>
    public static string? ToAbsoluteUrlOrNull(HttpRequest request, string? pathOrUrl)
        => string.IsNullOrEmpty(pathOrUrl) ? null : ToAbsoluteUrl(request, pathOrUrl);

    /// <summary>
    /// Maps a bare ISO-639-1 language code (e.g. <c>"de"</c>) to the OpenGraph-conventional
    /// <c>xx_XX</c> locale form used by <c>og:locale</c> (e.g. <c>"de_DE"</c>). <c>de</c> and
    /// <c>en</c> map to their well-known region variants (<c>de_DE</c>, <c>en_US</c>); any other
    /// two-letter code is paired with its upper-cased self as the region (e.g. <c>"fr"</c> →
    /// <c>"fr_FR"</c>). Anything else is returned unchanged.
    /// </summary>
    private static string ToOgLocale(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            return "en_US";

        var code = languageCode.Trim().ToLowerInvariant();
        return code switch
        {
            "de" => "de_DE",
            "en" => "en_US",
            _ when code.Length == 2 => $"{code}_{code.ToUpperInvariant()}",
            _ => languageCode,
        };
    }

    private static string BuildDescription(string? excerpt, string? content, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(excerpt))
            return excerpt;

        var stripped = StripHtml(content);
        return stripped.Length > 0 ? Truncate(stripped, 200) : fallback;
    }

    private static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "";
        return Regex.Replace(html, "<[^>]+>", " ").Trim();
    }

    private static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength)
            return text;
        return text[..maxLength].TrimEnd() + "…";
    }

    private static JsonObject BuildBreadcrumb(params (string Name, string Url)[] items)
    {
        var listItems = new JsonArray();
        for (var i = 0; i < items.Length; i++)
        {
            listItems.Add(new JsonObject
            {
                ["@type"] = "ListItem",
                ["position"] = i + 1,
                ["name"] = items[i].Name,
                ["item"] = items[i].Url,
            });
        }

        return new JsonObject
        {
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = listItems,
        };
    }

    private static string SerializeGraph(params JsonObject[] nodes)
    {
        var graph = new JsonArray();
        foreach (var node in nodes)
            graph.Add(node);

        var root = new JsonObject
        {
            ["@context"] = SchemaContext,
            ["@graph"] = graph,
        };

        return root.ToJsonString();
    }
}
