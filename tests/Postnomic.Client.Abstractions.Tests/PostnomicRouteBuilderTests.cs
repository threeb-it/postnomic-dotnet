using FluentAssertions;
using Postnomic.Client.Abstractions;
using Xunit;

namespace Postnomic.Client.Abstractions.Tests;

public class PostnomicRouteBuilderTests
{
    [Theory]
    [InlineData(PostnomicLanguageRouteStyle.Suffix, "de", "/blog/de/post/hello")]
    [InlineData(PostnomicLanguageRouteStyle.Prefix, "de", "/de/blog/post/hello")]
    [InlineData(PostnomicLanguageRouteStyle.None,   "de", "/blog/post/hello")]
    [InlineData(PostnomicLanguageRouteStyle.Suffix, null, "/blog/post/hello")]
    public void BuildPost_shapes(PostnomicLanguageRouteStyle style, string? lang, string expected)
        => PostnomicRouteBuilder.BuildPost("/blog", style, lang, "hello").Should().Be(expected);

    [Theory]
    [InlineData(PostnomicLanguageRouteStyle.Prefix, "/de/blog/post/x", "de")]
    [InlineData(PostnomicLanguageRouteStyle.Prefix, "/blog/post/x", null)]
    [InlineData(PostnomicLanguageRouteStyle.Suffix, "/blog/de/post/x", "de")]
    public void ExtractLang_works(PostnomicLanguageRouteStyle style, string path, string? expected)
        => PostnomicRouteBuilder.ExtractLang(path, "/blog", style).Should().Be(expected);

    [Theory]
    [InlineData(PostnomicLanguageRouteStyle.Prefix, "/de/blog", true)]
    [InlineData(PostnomicLanguageRouteStyle.Prefix, "/de/blog/post/x", true)]
    [InlineData(PostnomicLanguageRouteStyle.Prefix, "/other", false)]
    [InlineData(PostnomicLanguageRouteStyle.Suffix, "/blog/de", true)]
    public void MatchesBlog_works(PostnomicLanguageRouteStyle style, string path, bool expected)
        => PostnomicRouteBuilder.MatchesBlog(path, "/blog", style).Should().Be(expected);

    [Fact]
    public void BuildPostAlternates_NoAvailableLanguages_ReturnsEmpty()
        => PostnomicRouteBuilder
            .BuildPostAlternates("/blog", PostnomicLanguageRouteStyle.Prefix, [], "hello")
            .Should().BeEmpty();

    [Fact]
    public void BuildPostAlternates_PrefixStyle_AllLanguagesIncludingDefaultAreLanguagePrefixed()
    {
        // Under Prefix, the ONLY registered routes are /{lang}/blog/... — there is no bare /blog
        // route, so even the default language ("en", first entry) must carry its language segment.
        // Mapping it to a bare URL (as this test used to assert) would emit a 404ing hreflang
        // alternate.
        var alternates = PostnomicRouteBuilder.BuildPostAlternates(
            "/blog", PostnomicLanguageRouteStyle.Prefix, ["en", "de"], "hello");

        alternates.Should().BeEquivalentTo(new[]
        {
            ("en", "/en/blog/post/hello"),
            ("de", "/de/blog/post/hello"),
        });
    }

    [Fact]
    public void BuildPostAlternates_SuffixStyle_DefaultLanguageMapsToCanonicalUrl()
    {
        // Under Suffix, a bare /blog/post/... route legitimately exists and belongs to the default
        // language, so it continues to map there (unlike Prefix, above).
        var alternates = PostnomicRouteBuilder.BuildPostAlternates(
            "/blog", PostnomicLanguageRouteStyle.Suffix, ["en", "de"], "hello");

        alternates.Should().BeEquivalentTo(new[]
        {
            ("en", "/blog/post/hello"),
            ("de", "/blog/de/post/hello"),
        });
    }

    [Fact]
    public void BuildPostAlternates_SuffixStyle_DefaultLanguageMatchIsCaseInsensitive()
    {
        // AvailableLanguages sometimes come back with different casing than what's compared
        // against; the default-language match must not be case-sensitive. Only relevant to
        // Suffix/None, where the default language is the one case that maps to a different
        // (bare) URL than the rest — Prefix always uses BuildPost with the language's own code
        // regardless of default-match, so casing of that comparison can't affect its output.
        var alternates = PostnomicRouteBuilder.BuildPostAlternates(
            "/blog", PostnomicLanguageRouteStyle.Suffix, ["EN", "de"], "hello");

        alternates.Should().BeEquivalentTo(new[]
        {
            ("EN", "/blog/post/hello"),
            ("de", "/blog/de/post/hello"),
        });
    }

    [Fact]
    public void BuildPostAlternates_PrefixStyle_NoneOfTheAlternatesAreBareUrls()
    {
        // Guard test (elimination of the "bare URL in Prefix mode -> 404" bug class): whatever the
        // casing or ordering of AvailableLanguages, under Prefix every single alternate URL must
        // start with a /{lang}/ segment — none may be the bare /blog/... URL, since that route
        // isn't registered and would 404.
        var alternates = PostnomicRouteBuilder.BuildPostAlternates(
            "/blog", PostnomicLanguageRouteStyle.Prefix, ["de", "en"], "hello");

        alternates.Should().NotBeEmpty();
        alternates.Should().OnlyContain(a => a.Url.StartsWith($"/{a.Language}/blog/", StringComparison.OrdinalIgnoreCase));
        alternates.Select(a => a.Url).Should().NotContain("/blog/post/hello");
    }
}
