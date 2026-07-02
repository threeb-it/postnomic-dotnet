using FluentAssertions;
using Postnomic.Client.Abstractions.Models;
using Postnomic.Client.Abstractions.Seo;
using Xunit;

namespace Postnomic.Client.Abstractions.Tests;

/// <summary>
/// Unit tests for <see cref="PostnomicSeoBuilder"/> covering two final-review SEO-correctness
/// fixes: a consistent <c>x-default</c> hreflang target across a post's language cluster
/// (<see cref="PostnomicSeoModel.XDefaultUrl"/>), and UTC-normalization of
/// <see cref="PostnomicPostDetail.PublishedAt"/> for JSON-LD <c>datePublished</c> /
/// <c>article:published_time</c> (host-timezone independence, mirroring the same normalization
/// already applied to sitemap/RSS dates).
/// </summary>
public class PostnomicSeoBuilderTests
{
    private static PostnomicPostDetail CreatePost(
        string language = "de",
        IReadOnlyList<string>? availableLanguages = null,
        DateTime? publishedAt = null) => new()
    {
        Slug = "hello-world",
        Title = "Hello World",
        AuthorName = "Jane Doe",
        PublishedAt = publishedAt ?? new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
        Language = language,
        AvailableLanguages = availableLanguages ?? ["en", "de"],
    };

    // ── XDefaultUrl (Finding 2: consistent x-default across the language cluster) ─────────────

    [Fact]
    public void ForPost_XDefaultUrl_IsTheDefaultLanguageAlternate_NotTheCurrentPageCanonical()
    {
        // AvailableLanguages = ["en", "de"] => "en" is the default language (first entry), so its
        // alternate URL is what x-default should resolve to — even though this render is for the
        // "de" variant, whose own canonical is the /de/... URL. Under Prefix there is no bare
        // /blog/... route, so the default language's alternate is /en/blog/... (language-prefixed
        // like every other alternate), not a bare URL.
        var model = PostnomicSeoBuilder.ForPost(
            "https://example.com", "/blog", PostnomicLanguageRouteStyle.Prefix,
            lang: "de", postSlug: "hello-world", post: CreatePost(), blogInfo: null);

        model.CanonicalUrl.Should().Be("https://example.com/de/blog/post/hello-world");
        model.XDefaultUrl.Should().Be("https://example.com/en/blog/post/hello-world");
        model.XDefaultUrl.Should().NotBe(model.CanonicalUrl);
    }

    [Fact]
    public void ForPost_PrefixStyle_NoAlternateNorXDefaultIsABareUrl()
    {
        // Guard test (elimination of the "bare URL in Prefix mode -> 404" bug class): under
        // Prefix, the ONLY registered routes are /{lang}/blog/... — a bare /blog/post/... 404s.
        // Render both the de and en variants of a post with de+en available and assert none of the
        // emitted hreflang alternate URLs, nor XDefaultUrl, is ever the bare canonical.
        var bareUrl = "https://example.com/blog/post/hello-world";

        foreach (var lang in new[] { "de", "en" })
        {
            var model = PostnomicSeoBuilder.ForPost(
                "https://example.com", "/blog", PostnomicLanguageRouteStyle.Prefix,
                lang: lang, postSlug: "hello-world", post: CreatePost(), blogInfo: null);

            model.Alternates.Should().NotBeEmpty();
            model.Alternates.Select(a => a.Url).Should().NotContain(bareUrl);
            model.XDefaultUrl.Should().NotBe(bareUrl);
            model.Alternates.Should().OnlyContain(a => a.Url.StartsWith($"https://example.com/{a.Lang}/blog/"));
        }
    }

    [Fact]
    public void ForPost_XDefaultUrl_IsTheSameAcrossEveryLanguageVariantOfTheSamePost()
    {
        // The whole point of x-default is ONE consistent target for the entire de/en cluster, so
        // rendering the "de" and "en" variants of the same post must produce identical XDefaultUrl
        // even though their own CanonicalUrl values differ.
        var dePage = PostnomicSeoBuilder.ForPost(
            "https://example.com", "/blog", PostnomicLanguageRouteStyle.Prefix,
            lang: "de", postSlug: "hello-world", post: CreatePost(), blogInfo: null);
        var enPage = PostnomicSeoBuilder.ForPost(
            "https://example.com", "/blog", PostnomicLanguageRouteStyle.Prefix,
            lang: "en", postSlug: "hello-world", post: CreatePost(), blogInfo: null);

        dePage.CanonicalUrl.Should().NotBe(enPage.CanonicalUrl);
        dePage.XDefaultUrl.Should().Be(enPage.XDefaultUrl);
    }

    [Fact]
    public void XDefaultUrl_FallsBackToCanonicalUrl_WhenThereAreNoAlternates()
    {
        // ForIndex/ForAuthor only ever produce a single (current-language) alternate, and
        // BuildPostAlternates returns empty when a post has no AvailableLanguages — in both cases
        // there's no separate "default language" to point at, so x-default is just the canonical.
        var model = PostnomicSeoBuilder.ForPost(
            "https://example.com", "/blog", PostnomicLanguageRouteStyle.Prefix,
            lang: "de", postSlug: "hello-world",
            post: CreatePost(availableLanguages: []), blogInfo: null);

        model.Alternates.Should().BeEmpty();
        model.XDefaultUrl.Should().Be(model.CanonicalUrl);
    }

    // ── PublishedAt / datePublished UTC normalization (Finding 3) ──────────────────────────────

    [Fact]
    public void ForPost_UnspecifiedKindPublishedAt_JsonLdDatePublishedIsUtcNormalized()
    {
        // Mirrors what System.Text.Json produces for the API's zoneless "publishedAt" field.
        var unspecified = new DateTime(2026, 7, 2, 13, 30, 0, DateTimeKind.Unspecified);

        var model = PostnomicSeoBuilder.ForPost(
            "https://example.com", "/blog", PostnomicLanguageRouteStyle.Prefix,
            lang: "de", postSlug: "hello-world",
            post: CreatePost(publishedAt: unspecified), blogInfo: null);

        // The wall-clock value must be preserved (treated AS UTC, not converted from local time),
        // and the JSON-LD payload must carry a UTC designator so consumers can't misread it.
        model.JsonLd.Should().Contain("\"datePublished\":\"2026-07-02T13:30:00.0000000Z\"");
    }

    [Fact]
    public void ForPost_UnspecifiedKindPublishedAt_ModelPublishedAtIsUtcKind()
    {
        // Model.PublishedAt feeds article:published_time in both _SeoHead.cshtml and
        // PostnomicSeoHead.razor via publishedAt.ToString("O"); it must already be Utc-kind by the
        // time it reaches either renderer so both hosting models render a "Z"-suffixed timestamp
        // regardless of the host machine's local timezone.
        var unspecified = new DateTime(2026, 7, 2, 13, 30, 0, DateTimeKind.Unspecified);

        var model = PostnomicSeoBuilder.ForPost(
            "https://example.com", "/blog", PostnomicLanguageRouteStyle.Prefix,
            lang: "de", postSlug: "hello-world",
            post: CreatePost(publishedAt: unspecified), blogInfo: null);

        model.PublishedAt.Should().NotBeNull();
        model.PublishedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        model.PublishedAt.Value.ToString("O").Should().Be("2026-07-02T13:30:00.0000000Z");
    }

    [Fact]
    public void ForPost_AlreadyUtcPublishedAt_IsUnchanged()
    {
        var utc = new DateTime(2026, 7, 2, 13, 30, 0, DateTimeKind.Utc);

        var model = PostnomicSeoBuilder.ForPost(
            "https://example.com", "/blog", PostnomicLanguageRouteStyle.Prefix,
            lang: "de", postSlug: "hello-world",
            post: CreatePost(publishedAt: utc), blogInfo: null);

        model.PublishedAt.Should().Be(utc);
    }

    // ── ToAbsoluteUrl (Linux/Windows cross-platform regression guard) ──────────────────────────
    //
    // Uri.TryCreate(pathOrUrl, UriKind.Absolute, out _) is OS-DEPENDENT for a leading-slash
    // root-relative path: on Windows "/de/blog/post/x" is NOT parsed as absolute (correctly
    // falls through to base-prepending), but on Linux/Unix (CI + production Azure Container
    // Apps) the very same string IS parsed as an absolute "file:///de/blog/post/x" URI, so the
    // old check returned it unchanged — every canonical/og:url/hreflang/sitemap/RSS URL came out
    // relative in production. These tests assert the CORRECT (always-absolute) behavior and are
    // OS-independent themselves: they fail on the old code on Linux and pass on the fixed code on
    // both Windows and Linux.

    [Fact]
    public void ToAbsoluteUrl_RootRelativePath_PrependsBase_OnEveryOs()
    {
        // Regression guard for the Linux-only bug: the OLD implementation
        // (Uri.TryCreate(pathOrUrl, UriKind.Absolute, ...)) returns "/de/blog/post/x" UNCHANGED
        // on Linux because a leading "/" parses there as an absolute "file://" URI — silently
        // breaking every canonical/og:url/hreflang/sitemap/RSS URL in production.
        var result = PostnomicSeoBuilder.ToAbsoluteUrl("https://example.com", "/de/blog/post/x");

        result.Should().Be("https://example.com/de/blog/post/x");
    }

    [Fact]
    public void ToAbsoluteUrl_AlreadyAbsoluteHttpsUrl_IsUnchanged()
    {
        // Cover images from the API are absolute http(s) URLs and must pass through untouched.
        var result = PostnomicSeoBuilder.ToAbsoluteUrl(
            "https://example.com", "https://cdn.example.com/img.jpg");

        result.Should().Be("https://cdn.example.com/img.jpg");
    }

    [Fact]
    public void ToAbsoluteUrl_AlreadyAbsoluteHttpUrl_IsUnchanged()
    {
        var result = PostnomicSeoBuilder.ToAbsoluteUrl(
            "https://example.com", "http://cdn.example.com/img.jpg");

        result.Should().Be("http://cdn.example.com/img.jpg");
    }

    [Fact]
    public void ToAbsoluteUrl_ProtocolRelativeUrl_IsUnchanged()
    {
        var result = PostnomicSeoBuilder.ToAbsoluteUrl(
            "https://example.com", "//cdn.example.com/img.jpg");

        result.Should().Be("//cdn.example.com/img.jpg");
    }

    [Fact]
    public void ToAbsoluteUrl_RelativePathWithoutLeadingSlash_PrependsBaseWithSlash()
    {
        var result = PostnomicSeoBuilder.ToAbsoluteUrl("https://example.com", "blog/post/x");

        result.Should().Be("https://example.com/blog/post/x");
    }

    [Fact]
    public void ToAbsoluteUrl_TrailingSlashOnBase_IsTrimmed()
    {
        var result = PostnomicSeoBuilder.ToAbsoluteUrl("https://example.com/", "/de/blog/post/x");

        result.Should().Be("https://example.com/de/blog/post/x");
    }

    [Fact]
    public void ForPost_CanonicalUrl_IsAbsolute_OnEveryOs()
    {
        // End-to-end guard via the public ForPost API (mirrors how CanonicalUrl/OgUrl/hreflang
        // alternates are actually produced): the model's CanonicalUrl must always start with the
        // base's scheme, never with a bare "/", regardless of host OS.
        var model = PostnomicSeoBuilder.ForPost(
            "https://example.com", "/blog", PostnomicLanguageRouteStyle.Prefix,
            lang: "de", postSlug: "hello-world", post: CreatePost(), blogInfo: null);

        model.CanonicalUrl.Should().StartWith("https://example.com/");
    }
}
