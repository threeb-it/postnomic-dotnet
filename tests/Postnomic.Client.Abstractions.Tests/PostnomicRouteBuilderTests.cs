using Postnomic.Client.Abstractions;
using Xunit;

namespace Postnomic.Client.Abstractions.Tests;

public class PostnomicRouteBuilderTests
{
    [Theory]
    [InlineData(PostnomicLanguageRouteStyle.Suffix, "de", "/blog/de/post/hello")]
    [InlineData(PostnomicLanguageRouteStyle.Prefix, "de", "/de/blog/post/hello")]
    [InlineData(PostnomicLanguageRouteStyle.None, "de", "/blog/post/hello")]
    [InlineData(PostnomicLanguageRouteStyle.Suffix, null, "/blog/post/hello")]
    public void BuildPost_shapes(PostnomicLanguageRouteStyle style, string? lang, string expected)
        => Assert.Equal(expected, PostnomicRouteBuilder.BuildPost("/blog", style, lang, "hello"));

    [Theory]
    [InlineData(PostnomicLanguageRouteStyle.Prefix, "/de/blog/post/x", "de")]
    [InlineData(PostnomicLanguageRouteStyle.Prefix, "/blog/post/x", null)]
    [InlineData(PostnomicLanguageRouteStyle.Suffix, "/blog/de/post/x", "de")]
    public void ExtractLang_works(PostnomicLanguageRouteStyle style, string path, string? expected)
        => Assert.Equal(expected, PostnomicRouteBuilder.ExtractLang(path, "/blog", style));

    [Theory]
    [InlineData(PostnomicLanguageRouteStyle.Prefix, "/de/blog", true)]
    [InlineData(PostnomicLanguageRouteStyle.Prefix, "/de/blog/post/x", true)]
    [InlineData(PostnomicLanguageRouteStyle.Prefix, "/other", false)]
    [InlineData(PostnomicLanguageRouteStyle.Suffix, "/blog/de", true)]
    public void MatchesBlog_works(PostnomicLanguageRouteStyle style, string path, bool expected)
        => Assert.Equal(expected, PostnomicRouteBuilder.MatchesBlog(path, "/blog", style));

    [Fact]
    public void BuildPostAlternates_NoAvailableLanguages_ReturnsEmpty()
        => Assert.Empty(PostnomicRouteBuilder
            .BuildPostAlternates("/blog", PostnomicLanguageRouteStyle.Prefix, [], "hello"));

    [Fact]
    public void BuildPostAlternates_PrefixStyle_AllLanguagesIncludingDefaultAreLanguagePrefixed()
    {
        // Under Prefix, the ONLY registered routes are /{lang}/blog/... — there is no bare /blog
        // route, so even the default language ("en", first entry) must carry its language segment.
        // Mapping it to a bare URL (as this test used to assert) would emit a 404ing hreflang
        // alternate.
        var alternates = PostnomicRouteBuilder.BuildPostAlternates(
            "/blog", PostnomicLanguageRouteStyle.Prefix, ["en", "de"], "hello");

        Assert.Equal(new[]
        {
            ("en", "/en/blog/post/hello"),
            ("de", "/de/blog/post/hello"),
        }, alternates);
    }

    [Fact]
    public void BuildPostAlternates_SuffixStyle_DefaultLanguageMapsToCanonicalUrl()
    {
        // Under Suffix, a bare /blog/post/... route legitimately exists and belongs to the default
        // language, so it continues to map there (unlike Prefix, above).
        var alternates = PostnomicRouteBuilder.BuildPostAlternates(
            "/blog", PostnomicLanguageRouteStyle.Suffix, ["en", "de"], "hello");

        Assert.Equal(new[]
        {
            ("en", "/blog/post/hello"),
            ("de", "/blog/de/post/hello"),
        }, alternates);
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

        Assert.Equal(new[]
        {
            ("EN", "/blog/post/hello"),
            ("de", "/blog/de/post/hello"),
        }, alternates);
    }

    [Fact]
    public void BuildPostAlternates_NoneStyle_AllLanguagesCollapseOntoTheIdenticalBareUrl()
    {
        // Documented, not a defect: under None style NO language ever gets its own URL segment, so
        // this method composes the exact same bare URL for every language — it has no other basis
        // to compose from. Callers who need distinct per-language URLs here must supply them via
        // PostnomicClientOptions.AlternateUrlResolver instead of expecting this method to invent
        // one; PostnomicSeoBuilder.ForPost is what actually de-duplicates this collapsed output
        // before it reaches hreflang (see PostnomicSeoBuilderTests for that coverage).
        var alternates = PostnomicRouteBuilder.BuildPostAlternates(
            "/blog", PostnomicLanguageRouteStyle.None, ["de", "en"], "hello");

        Assert.Equal(new[]
        {
            ("de", "/blog/post/hello"),
            ("en", "/blog/post/hello"),
        }, alternates);
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

        Assert.NotEmpty(alternates);
        Assert.All(alternates, a => Assert.StartsWith($"/{a.Language}/blog/", a.Url, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("/blog/post/hello", alternates.Select(a => a.Url));
    }
}
