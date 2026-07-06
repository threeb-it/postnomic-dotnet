using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Postnomic.Client.Abstractions.Models;

namespace Postnomic.Client.Abstractions.Seo;

/// <summary>
/// Builds a <see cref="PostnomicSeoModel"/> (canonical/OpenGraph/Twitter/hreflang/JSON-LD data)
/// for a blog index, post, or author page, from raw domain data plus an absolute base URI
/// (e.g. <c>"https://example.com"</c> or <c>"https://example.com/"</c> — a trailing slash is
/// tolerated). This is the shared core used by both <c>Postnomic.Client.AspNetCore</c> (via
/// <c>Postnomic.Client.AspNetCore.Seo.PostnomicSeo</c>, which adapts the current HTTP request +
/// Razor Page models to these primitive inputs) and <c>Postnomic.Client.Blazor</c> (via
/// <c>NavigationManager.BaseUri</c>), so both hosting models emit identical SEO output instead of
/// each maintaining its own copy of the JSON-LD/meta-tag construction logic.
/// </summary>
public static class PostnomicSeoBuilder
{
    private const string SchemaContext = "https://schema.org";

    /// <summary>Builds the SEO model for a blog index (listing) page.</summary>
    public static PostnomicSeoModel ForIndex(
        string baseUri,
        string basePath,
        PostnomicLanguageRouteStyle style,
        string? lang,
        PostnomicBlogInfo? blogInfo,
        IEnumerable<PostnomicPostSummary> posts)
    {
        var canonical = ToAbsoluteUrl(baseUri, PostnomicRouteBuilder.BuildIndex(basePath, style, lang));
        var title = blogInfo?.Name ?? "Blog";
        var description = blogInfo?.Description;
        var effectiveLang = lang ?? "en";

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
        foreach (var post in posts)
        {
            itemListElements.Add(new JsonObject
            {
                ["@type"] = "ListItem",
                ["position"] = position++,
                ["url"] = ToAbsoluteUrl(baseUri, PostnomicRouteBuilder.BuildPost(basePath, style, lang, post.Slug)),
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
            Locale = ToOgLocale(effectiveLang),
            Alternates = [(effectiveLang, canonical)],
            JsonLd = SerializeGraph(blogNode, itemList, breadcrumb),
        };
    }

    /// <summary>Builds the SEO model for a single blog post page.</summary>
    public static PostnomicSeoModel ForPost(
        string baseUri,
        string basePath,
        PostnomicLanguageRouteStyle style,
        string? lang,
        string postSlug,
        PostnomicPostDetail post,
        PostnomicBlogInfo? blogInfo)
    {
        // Self-referential canonical: canonicalize to the URL of the language variant actually
        // being rendered (lang), not the blog's default-language URL.
        // Prefer the API-provided canonical (set only for cross-posted posts — points at the
        // primary blog); otherwise the self-referential canonical for the rendered language variant.
        var canonical = !string.IsNullOrWhiteSpace(post.CanonicalUrl)
            ? post.CanonicalUrl!
            : ToAbsoluteUrl(baseUri, PostnomicRouteBuilder.BuildPost(basePath, style, lang, postSlug));
        var image = ToAbsoluteUrlOrNull(baseUri, post.CoverImageUrl);
        var alternates = PostnomicRouteBuilder
            .BuildPostAlternates(basePath, style, post.AvailableLanguages, postSlug, post.Language)
            .Select(a => (a.Language, ToAbsoluteUrl(baseUri, a.Url)))
            .ToList();

        var description = BuildDescription(post.Excerpt, post.Content, post.Title);

        // The Postnomic API returns publishedAt without a trailing "Z"/offset, so
        // System.Text.Json deserializes it as DateTimeKind.Unspecified even though the value is
        // always already UTC (mirrors the same normalization PostnomicFeeds applies to
        // sitemap/RSS dates, so JSON-LD/OpenGraph timestamps agree with the feeds instead of
        // rendering a zoneless value that .ToString("O") would emit without a "Z"/offset).
        var publishedAtUtc = NormalizeToUtc(post.PublishedAt);

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
            ["datePublished"] = publishedAtUtc.ToString("O"),
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

        var indexUrl = ToAbsoluteUrl(baseUri, PostnomicRouteBuilder.BuildIndex(basePath, style, lang));
        var blogName = blogInfo?.Name ?? "Blog";
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
            PublishedAt = publishedAtUtc,
            AuthorName = post.AuthorName,
            Tags = post.Tags.Select(t => t.Name).ToList(),
        };
    }

    /// <summary>Builds the SEO model for an author profile page.</summary>
    public static PostnomicSeoModel ForAuthor(
        string baseUri,
        string basePath,
        PostnomicLanguageRouteStyle style,
        string? lang,
        string authorSlug,
        PostnomicAuthorProfile profile)
    {
        var canonical = ToAbsoluteUrl(baseUri, PostnomicRouteBuilder.BuildAuthor(basePath, style, lang, authorSlug));
        var image = ToAbsoluteUrlOrNull(baseUri, profile.ProfileImageUrl);
        var effectiveLang = lang ?? "en";

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

        var indexUrl = ToAbsoluteUrl(baseUri, PostnomicRouteBuilder.BuildIndex(basePath, style, lang));
        var breadcrumb = BuildBreadcrumb(("Blog", indexUrl), (profile.Name, canonical));

        return new PostnomicSeoModel
        {
            Title = profile.Name,
            Description = description,
            CanonicalUrl = canonical,
            ImageUrl = string.IsNullOrEmpty(image) ? null : image,
            OgType = "profile",
            SiteName = profile.Name,
            Locale = ToOgLocale(effectiveLang),
            Alternates = [(effectiveLang, canonical)],
            JsonLd = SerializeGraph(profilePage, breadcrumb),
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a path-relative or already-absolute, non-empty URL to an absolute URL using
    /// <paramref name="baseUri"/> (scheme + host, e.g. <c>"https://example.com"</c>; a trailing
    /// slash is tolerated and trimmed).
    /// </summary>
    public static string ToAbsoluteUrl(string baseUri, string pathOrUrl)
    {
        // NOTE: Do NOT use Uri.TryCreate(pathOrUrl, UriKind.Absolute, ...) here. On Unix/Linux
        // (CI + production Azure Container Apps), .NET's URI parser treats a leading-slash
        // root-relative path (e.g. "/de/blog/post/x") as an absolute "file:///..." URI, so the
        // check would incorrectly report it as already-absolute and skip prepending the base —
        // silently producing relative canonical/og:url/hreflang/sitemap/RSS URLs in production.
        // On Windows the same string is NOT parsed as absolute, so the bug is Linux-only and
        // invisible when developing/testing on Windows. Instead, explicitly recognize only the
        // forms that are genuinely already-absolute on every OS: http(s) URLs and
        // protocol-relative "//host/..." URLs.
        if (pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || pathOrUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return pathOrUrl;
        }

        var trimmedBase = baseUri.TrimEnd('/');
        return pathOrUrl.StartsWith('/') ? $"{trimmedBase}{pathOrUrl}" : $"{trimmedBase}/{pathOrUrl}";
    }

    /// <summary>
    /// Same as <see cref="ToAbsoluteUrl(string, string)"/> but returns <see langword="null"/>
    /// unchanged when <paramref name="pathOrUrl"/> is null or empty.
    /// </summary>
    public static string? ToAbsoluteUrlOrNull(string baseUri, string? pathOrUrl)
        => string.IsNullOrEmpty(pathOrUrl) ? null : ToAbsoluteUrl(baseUri, pathOrUrl);

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

    /// <summary>
    /// Normalizes a post timestamp to UTC without shifting its wall-clock value when the
    /// <see cref="DateTime.Kind"/> is <see cref="DateTimeKind.Unspecified"/>. The Postnomic API
    /// returns <c>publishedAt</c> without a trailing "Z"/offset, so System.Text.Json
    /// deserializes it as Unspecified even though the value is always already UTC; calling
    /// <c>.ToUniversalTime()</c> directly on it would misinterpret it as local time and shift it
    /// by the host machine's UTC offset. Already-Utc values pass through unchanged, and
    /// already-Local values still go through <c>.ToUniversalTime()</c> as intended. Shared with
    /// <c>Postnomic.Client.AspNetCore.Seo.PostnomicFeeds</c>, which applies the identical
    /// normalization to sitemap <c>&lt;lastmod&gt;</c> and RSS <c>&lt;pubDate&gt;</c>, so every
    /// emitted timestamp (feeds, JSON-LD, OpenGraph) is consistent regardless of host timezone.
    /// </summary>
    public static DateTime NormalizeToUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();

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
