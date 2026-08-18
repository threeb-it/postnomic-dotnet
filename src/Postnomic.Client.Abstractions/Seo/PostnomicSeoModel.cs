namespace Postnomic.Client.Abstractions.Seo;

/// <summary>
/// The data needed to render canonical/OpenGraph/Twitter/hreflang tags and a JSON-LD
/// structured-data script for a single blog page. Produced by <see cref="PostnomicSeoBuilder"/>
/// and consumed by both <c>Postnomic.Client.AspNetCore</c>'s <c>_SeoHead.cshtml</c> partial and
/// <c>Postnomic.Client.Blazor</c>'s <c>&lt;HeadContent&gt;</c> SEO blocks, so both hosting models
/// emit identical SEO output.
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
    /// language. Never <see langword="null"/>; may be empty. For post pages,
    /// <c>PostnomicSeoBuilder.ForPost</c> de-duplicates this list by URL — two languages that
    /// genuinely resolve to the same URL never both appear here, since a duplicate URL under two
    /// hreflang values isn't a meaningful language split (see <c>ForPost</c>'s XML docs for the
    /// full reasoning).
    /// </summary>
    public IReadOnlyList<(string Lang, string Url)> Alternates { get; init; } = [];

    /// <summary>
    /// The URL to use for the <c>hreflang="x-default"</c> alternate. Google expects exactly one
    /// consistent x-default target across an entire language cluster (e.g. the de and en variants
    /// of the same post), so this always resolves to the blog's/post's default-language URL
    /// (<see cref="Alternates"/>'s first entry, which <c>PostnomicSeoBuilder</c> always builds
    /// with the default language first) rather than the current page's own (per-language)
    /// <see cref="CanonicalUrl"/> — using <see cref="CanonicalUrl"/> here would make x-default
    /// differ between the de and en pages of the same cluster, which is incorrect. Falls back to
    /// <see cref="CanonicalUrl"/> when there are no alternates (e.g. a page with only one
    /// language).
    /// </summary>
    public string XDefaultUrl => Alternates.Count > 0 ? Alternates[0].Url : CanonicalUrl;

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
