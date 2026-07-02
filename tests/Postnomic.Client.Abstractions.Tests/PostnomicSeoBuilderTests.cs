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
        // alternate URL (bare, no lang segment) is what x-default should resolve to — even though
        // this render is for the "de" variant, whose own canonical is the /de/... URL.
        var model = PostnomicSeoBuilder.ForPost(
            "https://example.com", "/blog", PostnomicLanguageRouteStyle.Prefix,
            lang: "de", postSlug: "hello-world", post: CreatePost(), blogInfo: null);

        model.CanonicalUrl.Should().Be("https://example.com/de/blog/post/hello-world");
        model.XDefaultUrl.Should().Be("https://example.com/blog/post/hello-world");
        model.XDefaultUrl.Should().NotBe(model.CanonicalUrl);
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
}
