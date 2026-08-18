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

    /// <summary>
    /// Builds the SEO model for a single blog post page.
    /// </summary>
    /// <param name="baseUri">The blog's absolute base URI (scheme + host).</param>
    /// <param name="basePath">The base path the blog is served at (e.g. <c>"/blog"</c>).</param>
    /// <param name="style">The configured <see cref="PostnomicLanguageRouteStyle"/>.</param>
    /// <param name="lang">The language variant being rendered, or <see langword="null"/> for the blog's default language.</param>
    /// <param name="postSlug">The slug of the post being rendered.</param>
    /// <param name="post">The post's full detail, as returned by the API.</param>
    /// <param name="blogInfo">The blog's public metadata, used for <c>og:site_name</c> / the JSON-LD breadcrumb.</param>
    /// <param name="alternateUrls">
    /// Optional explicit (language, URL) pairs for this post's hreflang cluster, overriding
    /// <see cref="PostnomicRouteBuilder.BuildPostAlternates"/>'s composed alternates entirely when
    /// supplied. Pass <see langword="null"/> (the default) to keep the pre-existing
    /// composed-alternates behavior unchanged — every existing call site that doesn't pass this
    /// argument compiles and behaves exactly as before.
    /// <para>
    /// This exists because <c>BuildPostAlternates</c> can only ever apply one
    /// <see cref="PostnomicLanguageRouteStyle"/> to the SAME <paramref name="postSlug"/> across
    /// every language (see its own XML docs) — it has no way to know that a translation's real
    /// slug differs from the original post's, because <paramref name="post"/> carries no
    /// per-language slug field. Under <see cref="PostnomicLanguageRouteStyle.None"/> this is not
    /// a rare edge case: NO language ever gets its own URL segment there, so every composed
    /// alternate is the identical bare URL unless the host supplies the real ones here. Typically
    /// sourced from <see cref="PostnomicClientOptions.AlternateUrlResolver"/> by the two hosting
    /// models' adapters (<c>Postnomic.Client.AspNetCore.Seo.PostnomicSeo.ForPost</c> and
    /// <c>Postnomic.Client.Blazor</c>'s <c>PostPage</c>), so a host application only has to set
    /// that one option to affect both.
    /// </para>
    /// <para>
    /// <b>The shared-URL case.</b> When two or more languages genuinely resolve to the exact same
    /// URL (e.g. a post whose English and German editions were never split into separate URLs),
    /// this method keeps only the FIRST entry for that URL and silently drops the rest, whether
    /// they came from this override or from the composed fallback. Emitting
    /// <c>hreflang="de"</c> and <c>hreflang="en"</c> as two separate <c>&lt;link&gt;</c> tags that
    /// point at the identical URL is not meaningful markup: hreflang exists to tell a search
    /// engine about DIFFERENT URLs for different languages, and Google's own guidance is that it
    /// cannot infer a language split from a single crawled URL, no matter how many hreflang values
    /// claim otherwise. So rather than assert a false multi-URL cluster, this method treats that
    /// URL as belonging to whichever language reaches it first in list order — which, for both the
    /// composed fallback and a well-behaved <see cref="PostnomicClientOptions.AlternateUrlResolver"/>,
    /// is the blog's default language — keeping <see cref="PostnomicSeoModel.XDefaultUrl"/>
    /// coherent (its first-entry contract) as a side effect, without any extra bookkeeping.
    /// </para>
    /// </param>
    public static PostnomicSeoModel ForPost(
        string baseUri,
        string basePath,
        PostnomicLanguageRouteStyle style,
        string? lang,
        string postSlug,
        PostnomicPostDetail post,
        PostnomicBlogInfo? blogInfo,
        IReadOnlyList<(string Language, string Url)>? alternateUrls = null)
    {
        // Self-referential canonical: canonicalize to the URL of the language variant actually
        // being rendered (lang), not the blog's default-language URL.
        // Prefer the API-provided canonical (set only for cross-posted posts — points at the
        // primary blog); otherwise the self-referential canonical for the rendered language variant.
        var canonical = !string.IsNullOrWhiteSpace(post.CanonicalUrl)
            ? post.CanonicalUrl!
            : ToAbsoluteUrl(baseUri, PostnomicRouteBuilder.BuildPost(basePath, style, lang, postSlug));
        var image = ToAbsoluteUrlOrNull(baseUri, post.CoverImageUrl);
        var rawAlternates = alternateUrls ?? PostnomicRouteBuilder
            .BuildPostAlternates(basePath, style, post.AvailableLanguages, postSlug, post.Language);
        var alternates = DeduplicateAlternatesByUrl(
            rawAlternates.Select(a => (a.Language, ToAbsoluteUrl(baseUri, a.Url))));

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
    /// Collapses a sequence of (language, absolute URL) alternates so no two entries share the
    /// same URL, keeping the FIRST occurrence and dropping the rest. See the "shared-URL case"
    /// remarks on <see cref="ForPost"/> for why: a duplicate URL under two different hreflang
    /// values isn't a real language split, it's the same document claimed twice, and Google can't
    /// tell the two apart from a single crawled URL regardless of how many hreflang links point at
    /// it. Comparison is ordinal case-insensitive since these are already-absolute URLs.
    /// </summary>
    private static List<(string Language, string Url)> DeduplicateAlternatesByUrl(
        IEnumerable<(string Language, string Url)> alternates)
    {
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deduplicated = new List<(string Language, string Url)>();
        foreach (var alternate in alternates)
        {
            if (seenUrls.Add(alternate.Url))
                deduplicated.Add(alternate);
        }

        return deduplicated;
    }

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

    // Meta descriptions target Google's practical display limit of ~155-160 characters; longer
    // than that gets truncated in the SERP anyway, so 160 is where WE truncate on our own terms
    // (a whole word, an ellipsis) rather than letting Google cut mid-word.
    private const int DescriptionMaxLength = 160;

    private static readonly Regex s_codeFence = new(@"```.*?```", RegexOptions.Singleline);
    private static readonly Regex s_inlineCode = new(@"`[^`\r\n]*`");
    private static readonly Regex s_markdownImage = new(@"!\[[^\]]*\]\([^)]*\)");
    private static readonly Regex s_markdownLink = new(@"\[([^\]]*)\]\([^)]*\)");
    private static readonly Regex s_heading = new(@"^ {0,3}#{1,6} +", RegexOptions.Multiline);
    private static readonly Regex s_blockquote = new(@"^ {0,3}(?:> ?)+", RegexOptions.Multiline);
    private static readonly Regex s_listMarker = new(@"^ {0,3}(?:[-*+]|\d+\.) +", RegexOptions.Multiline);
    private static readonly Regex s_boldEmphasis = new(@"(\*\*|__)(.+?)\1");
    private static readonly Regex s_strikethrough = new(@"~~(.+?)~~");
    private static readonly Regex s_italicEmphasis = new(@"(?<!\w)(\*|_)(.+?)\1(?!\w)");
    private static readonly Regex s_htmlTag = new("<[^>]+>");
    private static readonly Regex s_whitespaceRun = new(@"\s+");

    /// <summary>
    /// Builds the meta/OpenGraph/Twitter description for a post.
    /// <para>
    /// An explicit <paramref name="excerpt"/> (author-supplied front matter) is always used
    /// verbatim, untruncated — it's deliberate authored content, not something this builder should
    /// silently mutate. A very long excerpt is the author's own call, not a defect here; a host
    /// that wants a hard limit enforced can truncate <see cref="PostnomicPostDetail.Excerpt"/>
    /// itself before it reaches this method.
    /// </para>
    /// <para>
    /// Without an excerpt, this falls back to the post's own <paramref name="content"/>, which may
    /// be Markdown OR HTML (posts in this SDK aren't guaranteed to be one or the other) — stripped
    /// of both, whitespace-collapsed onto one line, freed of a leading repetition of the post's own
    /// <paramref name="title"/> (a Markdown H1 duplicating the title is a common authoring pattern
    /// and reads badly appended right after the real &lt;title&gt;/og:title), and truncated at a
    /// word boundary to <see cref="DescriptionMaxLength"/> characters. Markdown images are removed
    /// ENTIRELY, alt text included — alt text describes a picture, it doesn't summarize the post,
    /// and leaving it in is exactly the "stray '!' followed by someone else's alt text" bug this
    /// method exists to fix. Markdown links keep their visible text and drop the URL.
    /// </para>
    /// </summary>
    private static string BuildDescription(string? excerpt, string? content, string title)
    {
        if (!string.IsNullOrWhiteSpace(excerpt))
            return excerpt;

        var plain = s_whitespaceRun.Replace(StripMarkdownAndHtml(content), " ").Trim();
        plain = RemoveLeadingDuplicateTitle(plain, title);

        return plain.Length > 0 ? TruncateAtWordBoundary(plain, DescriptionMaxLength) : title;
    }

    /// <summary>
    /// Strips Markdown constructs (code fences/spans, images, links, headings, blockquotes, list
    /// markers, bold/italic/strikethrough emphasis) as well as any literal HTML tags, in that
    /// order — images and links are resolved before emphasis stripping so that e.g. a bold link's
    /// <c>**</c> markers, once exposed by the link regex keeping only its text, still get cleaned
    /// up by the emphasis pass that follows. Headings/blockquotes/list markers are line-anchored,
    /// so this must run before whitespace is collapsed elsewhere (their regexes rely on the
    /// original newlines to find each line's start). Returns "" for null/whitespace-only input;
    /// otherwise the surviving plain text, NOT yet whitespace-collapsed or trimmed.
    /// </summary>
    private static string StripMarkdownAndHtml(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var result = s_codeFence.Replace(text, " ");
        result = s_inlineCode.Replace(result, " ");
        result = s_markdownImage.Replace(result, " ");
        result = s_markdownLink.Replace(result, "$1");
        result = s_heading.Replace(result, "");
        result = s_blockquote.Replace(result, "");
        result = s_listMarker.Replace(result, "");
        result = s_boldEmphasis.Replace(result, "$2");
        result = s_strikethrough.Replace(result, "$1");
        result = s_italicEmphasis.Replace(result, "$2");
        return s_htmlTag.Replace(result, " ");
    }

    /// <summary>
    /// Drops a leading occurrence of <paramref name="title"/> from <paramref name="text"/>
    /// (case-insensitive), along with a single separator character the two were likely joined by
    /// (a colon, dash, en/em dash, period, or plain whitespace) — the shape left behind by a
    /// Markdown H1 that repeats the post's own title once its <c>#</c> marker is stripped.
    /// </summary>
    private static string RemoveLeadingDuplicateTitle(string text, string title)
    {
        if (string.IsNullOrWhiteSpace(title) || !text.StartsWith(title, StringComparison.OrdinalIgnoreCase))
            return text;

        return text[title.Length..].TrimStart(' ', ':', '-', '–', '—', '.').TrimStart();
    }

    private static string TruncateAtWordBoundary(string text, int maxLength)
    {
        if (text.Length <= maxLength)
            return text;

        var cut = text[..maxLength];
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > 0)
            cut = cut[..lastSpace];

        return cut.TrimEnd() + "…";
    }

    private static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "";
        return s_htmlTag.Replace(html, " ").Trim();
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
