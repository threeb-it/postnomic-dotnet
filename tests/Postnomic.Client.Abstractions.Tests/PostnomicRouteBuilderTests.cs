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
    public void BuildPostAlternates_PrefixStyle_DefaultLanguageMapsToCanonicalUrl()
    {
        var alternates = PostnomicRouteBuilder.BuildPostAlternates(
            "/blog", PostnomicLanguageRouteStyle.Prefix, ["en", "de"], "hello");

        alternates.Should().BeEquivalentTo(new[]
        {
            ("en", "/blog/post/hello"),
            ("de", "/de/blog/post/hello"),
        });
    }

    [Fact]
    public void BuildPostAlternates_SuffixStyle_DefaultLanguageMapsToCanonicalUrl()
    {
        var alternates = PostnomicRouteBuilder.BuildPostAlternates(
            "/blog", PostnomicLanguageRouteStyle.Suffix, ["en", "de"], "hello");

        alternates.Should().BeEquivalentTo(new[]
        {
            ("en", "/blog/post/hello"),
            ("de", "/blog/de/post/hello"),
        });
    }

    [Fact]
    public void BuildPostAlternates_DefaultLanguageMatchIsCaseInsensitive()
    {
        // AvailableLanguages sometimes come back with different casing than what's compared
        // against; the default-language match must not be case-sensitive.
        var alternates = PostnomicRouteBuilder.BuildPostAlternates(
            "/blog", PostnomicLanguageRouteStyle.Prefix, ["EN", "de"], "hello");

        alternates.Should().BeEquivalentTo(new[]
        {
            ("EN", "/blog/post/hello"),
            ("de", "/de/blog/post/hello"),
        });
    }
}
